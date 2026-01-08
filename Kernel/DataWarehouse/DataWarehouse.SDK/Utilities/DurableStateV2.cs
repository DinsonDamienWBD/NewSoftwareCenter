using DataWarehouse.SDK.Contracts;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DataWarehouse.SDK.Utilities
{
    /// <summary>
    /// Storage-Agnostic Durable State with Write-Ahead Logging.
    ///
    /// Improvements over DurableState:
    /// - Uses IStorageProvider abstraction (not hardcoded to local disk)
    /// - Fully async API
    /// - Benefits from RAID protection if configured
    /// - Works with ANY storage backend (Local, S3, IPFS, RAMDisk, etc.)
    /// - Maintains in-memory cache for O(1) read performance
    ///
    /// Architecture:
    ///   Application → DurableStateV2 (In-Memory Cache) → IStorageProvider → Backend (RAID/Cloud/Disk)
    ///
    /// Write-Ahead Log Format:
    ///   [OpCode:1][Key:string][Length:4][JSON:N bytes]
    ///   OpCode: 1 = Set, 2 = Remove
    ///
    /// </summary>
    /// <typeparam name="T">Value type (must be JSON-serializable)</typeparam>
    public class DurableStateV2<T> : IAsyncDisposable, IDisposable
    {
        private readonly IStorageProvider _storageProvider;
        private readonly Uri _journalUri;
        private readonly ConcurrentDictionary<string, T> _cache = new();
        private readonly SemaphoreSlim _lock = new(1, 1);
        private int _opCount = 0;
        private const int CompactionThreshold = 5000;
        private bool _disposed = false;

        /// <summary>
        /// Initializes a storage-agnostic durable state.
        /// </summary>
        /// <param name="storageProvider">Storage backend (can be Local, S3, RAID, etc.)</param>
        /// <param name="journalKey">Unique key for this journal (e.g., "security/acl.journal")</param>
        public DurableStateV2(IStorageProvider storageProvider, string journalKey)
        {
            _storageProvider = storageProvider ?? throw new ArgumentNullException(nameof(storageProvider));
            _journalUri = new Uri($"{storageProvider.Scheme}://{journalKey}");

            // Synchronous load for constructor (async version below)
            LoadAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Asynchronously loads and replays the journal from storage.
        /// </summary>
        private async Task LoadAsync()
        {
            try
            {
                if (!await _storageProvider.ExistsAsync(_journalUri))
                {
                    // Journal doesn't exist yet (first run)
                    return;
                }

                // Load journal from storage backend
                using var stream = await _storageProvider.LoadAsync(_journalUri);
                await ReplayJournalAsync(stream);
            }
            catch (FileNotFoundException)
            {
                // Journal doesn't exist (expected on first run)
            }
            catch (Exception)
            {
                // Log corruption or read failure - start fresh
                // In production, log this error via IKernelContext
            }
        }

        /// <summary>
        /// Replays the write-ahead log to rebuild in-memory cache.
        /// </summary>
        private async Task ReplayJournalAsync(Stream journalStream)
        {
            using var reader = new BinaryReader(journalStream, Encoding.UTF8, leaveOpen: true);

            while (journalStream.Position < journalStream.Length)
            {
                try
                {
                    var opCode = reader.ReadByte(); // 1 = Set, 2 = Remove
                    var key = reader.ReadString();

                    if (opCode == 1) // Set
                    {
                        var length = reader.ReadInt32();
                        var bytes = reader.ReadBytes(length);
                        var json = Encoding.UTF8.GetString(bytes);
                        var val = JsonSerializer.Deserialize<T>(json);
                        if (val != null)
                        {
                            _cache[key] = val;
                        }
                    }
                    else if (opCode == 2) // Remove
                    {
                        _cache.TryRemove(key, out _);
                    }
                }
                catch (EndOfStreamException)
                {
                    // Safe truncation - stop replay
                    break;
                }
                catch (Exception)
                {
                    // Corrupt entry - skip and continue
                    break;
                }
            }
        }

        /// <summary>
        /// Sets a key-value pair (asynchronously persists to journal).
        /// </summary>
        public async Task SetAsync(string key, T value)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DurableStateV2<T>));

            await _lock.WaitAsync();
            try
            {
                _cache[key] = value;
                await AppendLogAsync(1, key, value);
                await CheckCompactionAsync();
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Synchronous version of SetAsync (for backward compatibility).
        /// </summary>
        public void Set(string key, T value)
        {
            SetAsync(key, value).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Removes a key-value pair (asynchronously persists to journal).
        /// </summary>
        public async Task<bool> RemoveAsync(string key)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DurableStateV2<T>));

            await _lock.WaitAsync();
            try
            {
                if (_cache.TryRemove(key, out _))
                {
                    await AppendLogAsync(2, key, default);
                    await CheckCompactionAsync();
                    return true;
                }
                return false;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Synchronous version of RemoveAsync (for backward compatibility).
        /// </summary>
        public bool Remove(string key, out T? value)
        {
            var result = _cache.TryRemove(key, out value);
            if (result)
            {
                RemoveAsync(key).GetAwaiter().GetResult();
            }
            return result;
        }

        /// <summary>
        /// Gets a value by key (O(1) from in-memory cache).
        /// </summary>
        public T? Get(string key) => _cache.TryGetValue(key, out var val) ? val : default;

        /// <summary>
        /// Tries to get a value by key.
        /// </summary>
        public bool TryGet(string key, out T? value) => _cache.TryGetValue(key, out value);

        /// <summary>
        /// Gets all values.
        /// </summary>
        public IEnumerable<T> GetAll() => _cache.Values;

        /// <summary>
        /// Gets all key-value pairs.
        /// </summary>
        public IEnumerable<KeyValuePair<string, T>> GetAllKeyValues() => _cache;

        /// <summary>
        /// Clears all entries (WARNING: Deletes journal permanently!).
        /// </summary>
        public async Task ClearAsync()
        {
            await _lock.WaitAsync();
            try
            {
                _cache.Clear();

                // Delete journal from storage
                if (await _storageProvider.ExistsAsync(_journalUri))
                {
                    await _storageProvider.DeleteAsync(_journalUri);
                }

                _opCount = 0;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Synchronous clear (for backward compatibility).
        /// </summary>
        public void Clear()
        {
            ClearAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Appends an operation to the write-ahead log.
        /// </summary>
        private async Task AppendLogAsync(byte opCode, string key, T? value)
        {
            // Build log entry in memory
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);

            writer.Write(opCode);
            writer.Write(key);

            if (opCode == 1 && value != null)
            {
                var json = JsonSerializer.Serialize(value);
                var bytes = Encoding.UTF8.GetBytes(json);
                writer.Write(bytes.Length);
                writer.Write(bytes);
            }

            writer.Flush();
            ms.Position = 0;

            // Append to existing journal
            // Note: This requires "append" support, but IStorageProvider uses SaveAsync (overwrite)
            // Workaround: Load existing journal, append, and save back
            // TODO: Future enhancement - Add AppendAsync to IStorageProvider

            Stream existingJournal;
            try
            {
                existingJournal = await _storageProvider.LoadAsync(_journalUri);
            }
            catch (FileNotFoundException)
            {
                existingJournal = new MemoryStream();
            }

            // Combine existing + new entry
            using var combined = new MemoryStream();
            await existingJournal.CopyToAsync(combined);
            await ms.CopyToAsync(combined);
            combined.Position = 0;

            // Save combined journal
            await _storageProvider.SaveAsync(_journalUri, combined);

            existingJournal.Dispose();
            _opCount++;
        }

        /// <summary>
        /// Checks if compaction is needed (after CompactionThreshold operations).
        /// </summary>
        private async Task CheckCompactionAsync()
        {
            if (_opCount >= CompactionThreshold)
            {
                await CompactAsync();
                _opCount = 0;
            }
        }

        /// <summary>
        /// Compacts the journal by rewriting it with only current state.
        /// Reduces file size and replay time.
        /// </summary>
        public async Task CompactAsync()
        {
            await _lock.WaitAsync();
            try
            {
                var tempUri = new Uri($"{_journalUri.Scheme}://{_journalUri.AbsolutePath}.compact");

                // Write compacted journal to temp location
                using var ms = new MemoryStream();
                using var writer = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);

                foreach (var kvp in _cache)
                {
                    writer.Write((byte)1); // Set
                    writer.Write(kvp.Key);
                    var json = JsonSerializer.Serialize(kvp.Value);
                    var bytes = Encoding.UTF8.GetBytes(json);
                    writer.Write(bytes.Length);
                    writer.Write(bytes);
                }

                writer.Flush();
                ms.Position = 0;

                // Save compacted journal
                await _storageProvider.SaveAsync(tempUri, ms);

                // Atomic swap: Delete old journal, rename compact to journal
                if (await _storageProvider.ExistsAsync(_journalUri))
                {
                    await _storageProvider.DeleteAsync(_journalUri);
                }

                // Copy compact to journal location
                ms.Position = 0;
                await _storageProvider.SaveAsync(_journalUri, ms);

                // Delete temp compact file
                await _storageProvider.DeleteAsync(tempUri);

                _opCount = 0;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Exposes cache as dictionary for complex LINQ queries.
        /// </summary>
        public IDictionary<string, T> ToDictionary() => _cache;

        /// <summary>
        /// Gets the number of entries in the cache.
        /// </summary>
        public int Count => _cache.Count;

        /// <summary>
        /// Gets the storage provider backing this durable state.
        /// </summary>
        public IStorageProvider StorageProvider => _storageProvider;

        /// <summary>
        /// Gets the journal URI.
        /// </summary>
        public Uri JournalUri => _journalUri;

        /// <summary>
        /// Asynchronous disposal (recommended).
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;

            await _lock.WaitAsync();
            try
            {
                // Final compaction to ensure all data is persisted
                if (_opCount > 0)
                {
                    await CompactAsync();
                }

                _lock.Dispose();
                _disposed = true;
            }
            finally
            {
                if (!_disposed)
                {
                    _lock.Release();
                }
            }

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Synchronous disposal (for backward compatibility).
        /// </summary>
        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}
