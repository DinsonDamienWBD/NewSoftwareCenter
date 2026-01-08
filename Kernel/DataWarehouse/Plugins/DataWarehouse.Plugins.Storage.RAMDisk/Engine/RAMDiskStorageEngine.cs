using DataWarehouse.SDK.Contracts;
using System.Collections.Concurrent;
using System.Text.Json;

namespace DataWarehouse.Plugins.Storage.RAMDisk.Engine
{
    /// <summary>
    /// High-performance in-memory storage engine with optional persistence.
    /// Thread-safe, supports memory limits with LRU eviction.
    /// </summary>
    public class RAMDiskStorageEngine : IStorageProvider, IDisposable
    {
        public string Id { get; private set; }
        public string Version => "1.0.0";
        public string Name => "RAMDisk Storage";
        public string Scheme => "ramdisk";

        private readonly ConcurrentDictionary<string, byte[]> _storage;
        private readonly ConcurrentDictionary<string, AccessInfo> _accessTracking;
        private readonly object _evictionLock = new();
        private IKernelContext? _context;

        // Configuration
        private long _maxMemoryBytes = 1024L * 1024L * 1024L; // 1GB default
        private string? _persistencePath;
        private TimeSpan _autoSaveInterval = TimeSpan.FromMinutes(5);
        private Timer? _autoSaveTimer;
        private bool _isDisposed;

        // Metrics
        private long _currentMemoryUsage;
        private long _totalReads;
        private long _totalWrites;
        private long _totalEvictions;

        /// <summary>
        /// Access tracking for LRU eviction.
        /// </summary>
        private class AccessInfo
        {
            public DateTime LastAccessTime { get; set; }
            public long AccessCount { get; set; }
            public long Size { get; set; }
        }

        public RAMDiskStorageEngine()
        {
            Id = "ramdisk-default";
            _storage = new ConcurrentDictionary<string, byte[]>();
            _accessTracking = new ConcurrentDictionary<string, AccessInfo>();
        }

        /// <summary>
        /// Initialize the RAMDisk storage provider.
        /// </summary>
        public void Initialize(IKernelContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));

            // Load configuration
            var maxMemoryMB = Environment.GetEnvironmentVariable("DW_RAMDISK_MAX_MEMORY_MB");
            if (!string.IsNullOrEmpty(maxMemoryMB) && long.TryParse(maxMemoryMB, out var maxMB))
            {
                _maxMemoryBytes = maxMB * 1024L * 1024L;
            }

            _persistencePath = Environment.GetEnvironmentVariable("DW_RAMDISK_PERSISTENCE_PATH");

            var autoSaveMinutes = Environment.GetEnvironmentVariable("DW_RAMDISK_AUTOSAVE_MINUTES");
            if (!string.IsNullOrEmpty(autoSaveMinutes) && int.TryParse(autoSaveMinutes, out var minutes))
            {
                _autoSaveInterval = TimeSpan.FromMinutes(minutes);
            }

            _context.LogInfo($"[{Id}] Initialized RAMDisk: MaxMemory={_maxMemoryBytes / 1024 / 1024}MB, Persistence={_persistencePath ?? "None"}");

            // Load from persistence if configured
            if (!string.IsNullOrEmpty(_persistencePath) && File.Exists(_persistencePath))
            {
                LoadFromDiskAsync().GetAwaiter().GetResult();
            }

            // Start auto-save timer if persistence is configured
            if (!string.IsNullOrEmpty(_persistencePath) && _autoSaveInterval > TimeSpan.Zero)
            {
                _autoSaveTimer = new Timer(
                    async _ => await SaveToDiskAsync(),
                    null,
                    _autoSaveInterval,
                    _autoSaveInterval
                );
            }
        }

        /// <summary>
        /// Save blob to RAMDisk.
        /// </summary>
        public async Task SaveAsync(Uri uri, Stream data)
        {
            if (uri == null)
                throw new ArgumentNullException(nameof(uri));
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            try
            {
                // Read data into memory
                byte[] buffer;
                using (var ms = new MemoryStream())
                {
                    await data.CopyToAsync(ms);
                    buffer = ms.ToArray();
                }

                var key = uri.ToString();
                var size = buffer.Length;

                // Check if we need to evict before adding
                while (_currentMemoryUsage + size > _maxMemoryBytes)
                {
                    if (!EvictLeastRecentlyUsed())
                    {
                        throw new InvalidOperationException($"Cannot store {size} bytes: Memory limit reached and no items to evict");
                    }
                }

                // Store data
                _storage[key] = buffer;

                // Update access tracking
                _accessTracking[key] = new AccessInfo
                {
                    LastAccessTime = DateTime.UtcNow,
                    AccessCount = 0,
                    Size = size
                };

                // Update metrics
                Interlocked.Add(ref _currentMemoryUsage, size);
                Interlocked.Increment(ref _totalWrites);

                _context?.LogInfo($"[RAMDisk] Stored {size} bytes at {key}");

                // Emit event
                _context?.PublishAsync("BlobStored", new
                {
                    Provider = "ramdisk",
                    Uri = key,
                    Size = size,
                    Timestamp = DateTime.UtcNow
                }).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _context?.LogError($"[RAMDisk] Error storing blob", ex);
                throw;
            }
        }

        /// <summary>
        /// Load blob from RAMDisk.
        /// </summary>
        public async Task<Stream> LoadAsync(Uri uri)
        {
            if (uri == null)
                throw new ArgumentNullException(nameof(uri));

            try
            {
                var key = uri.ToString();

                if (!_storage.TryGetValue(key, out var data))
                {
                    throw new FileNotFoundException($"Blob not found: {key}");
                }

                // Update access tracking
                if (_accessTracking.TryGetValue(key, out var accessInfo))
                {
                    accessInfo.LastAccessTime = DateTime.UtcNow;
                    accessInfo.AccessCount++;
                }

                // Update metrics
                Interlocked.Increment(ref _totalReads);

                _context?.LogInfo($"[RAMDisk] Loaded {data.Length} bytes from {key}");

                // Emit event
                await (_context?.PublishAsync("BlobAccessed", new
                {
                    Provider = "ramdisk",
                    Uri = key,
                    Size = data.Length,
                    Timestamp = DateTime.UtcNow
                }) ?? Task.CompletedTask);

                // Return copy of data as stream
                return new MemoryStream(data, writable: false);
            }
            catch (Exception ex)
            {
                _context?.LogError($"[RAMDisk] Error loading blob", ex);
                throw;
            }
        }

        /// <summary>
        /// Delete blob from RAMDisk.
        /// </summary>
        public async Task DeleteAsync(Uri uri)
        {
            if (uri == null)
                throw new ArgumentNullException(nameof(uri));

            try
            {
                var key = uri.ToString();

                if (_storage.TryRemove(key, out var data))
                {
                    _accessTracking.TryRemove(key, out _);
                    Interlocked.Add(ref _currentMemoryUsage, -data.Length);

                    _context?.LogInfo($"[RAMDisk] Deleted {data.Length} bytes from {key}");

                    // Emit event
                    await (_context?.PublishAsync("BlobDeleted", new
                    {
                        Provider = "ramdisk",
                        Uri = key,
                        Timestamp = DateTime.UtcNow
                    }) ?? Task.CompletedTask);
                }
            }
            catch (Exception ex)
            {
                _context?.LogError($"[RAMDisk] Error deleting blob", ex);
                throw;
            }
        }

        /// <summary>
        /// Check if blob exists in RAMDisk.
        /// </summary>
        public Task<bool> ExistsAsync(Uri uri)
        {
            if (uri == null)
                throw new ArgumentNullException(nameof(uri));

            var key = uri.ToString();
            return Task.FromResult(_storage.ContainsKey(key));
        }

        /// <summary>
        /// Evict least recently used item.
        /// </summary>
        private bool EvictLeastRecentlyUsed()
        {
            lock (_evictionLock)
            {
                if (_accessTracking.IsEmpty)
                    return false;

                // Find LRU item
                var lruKey = _accessTracking
                    .OrderBy(kvp => kvp.Value.LastAccessTime)
                    .ThenBy(kvp => kvp.Value.AccessCount)
                    .FirstOrDefault().Key;

                if (lruKey == null)
                    return false;

                // Remove item
                if (_storage.TryRemove(lruKey, out var data))
                {
                    _accessTracking.TryRemove(lruKey, out _);
                    Interlocked.Add(ref _currentMemoryUsage, -data.Length);
                    Interlocked.Increment(ref _totalEvictions);

                    _context?.LogWarning($"[RAMDisk] Evicted {data.Length} bytes from {lruKey}");
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Save RAMDisk contents to disk file.
        /// </summary>
        public async Task SaveToDiskAsync()
        {
            if (string.IsNullOrEmpty(_persistencePath))
                return;

            try
            {
                _context?.LogInfo($"[RAMDisk] Saving to disk: {_persistencePath}");

                var snapshot = new Dictionary<string, byte[]>();
                foreach (var kvp in _storage)
                {
                    snapshot[kvp.Key] = kvp.Value;
                }

                var json = JsonSerializer.Serialize(snapshot);
                var compressed = System.IO.Compression.GZipStream.CompressAsync(
                    new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json))
                );

                // Ensure directory exists
                var directory = Path.GetDirectoryName(_persistencePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Write to temp file first, then rename (atomic operation)
                var tempPath = _persistencePath + ".tmp";
                await File.WriteAllBytesAsync(tempPath, await ReadAllBytesAsync(compressed));
                File.Move(tempPath, _persistencePath, overwrite: true);

                _context?.LogInfo($"[RAMDisk] Saved {snapshot.Count} items to disk");
            }
            catch (Exception ex)
            {
                _context?.LogError($"[RAMDisk] Error saving to disk", ex);
            }
        }

        /// <summary>
        /// Load RAMDisk contents from disk file.
        /// </summary>
        private async Task LoadFromDiskAsync()
        {
            if (string.IsNullOrEmpty(_persistencePath) || !File.Exists(_persistencePath))
                return;

            try
            {
                _context?.LogInfo($"[RAMDisk] Loading from disk: {_persistencePath}");

                var compressed = await File.ReadAllBytesAsync(_persistencePath);
                var decompressed = await System.IO.Compression.GZipStream.UncompressAsync(
                    new MemoryStream(compressed)
                );

                var json = System.Text.Encoding.UTF8.GetString(await ReadAllBytesAsync(decompressed));
                var snapshot = JsonSerializer.Deserialize<Dictionary<string, byte[]>>(json);

                if (snapshot != null)
                {
                    long loadedBytes = 0;
                    foreach (var kvp in snapshot)
                    {
                        _storage[kvp.Key] = kvp.Value;
                        _accessTracking[kvp.Key] = new AccessInfo
                        {
                            LastAccessTime = DateTime.UtcNow,
                            AccessCount = 0,
                            Size = kvp.Value.Length
                        };
                        loadedBytes += kvp.Value.Length;
                    }

                    Interlocked.Exchange(ref _currentMemoryUsage, loadedBytes);
                    _context?.LogInfo($"[RAMDisk] Loaded {snapshot.Count} items ({loadedBytes / 1024 / 1024}MB) from disk");
                }
            }
            catch (Exception ex)
            {
                _context?.LogError($"[RAMDisk] Error loading from disk", ex);
            }
        }

        /// <summary>
        /// Helper to read all bytes from stream.
        /// </summary>
        private static async Task<byte[]> ReadAllBytesAsync(Stream stream)
        {
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            return ms.ToArray();
        }

        /// <summary>
        /// Get current statistics.
        /// </summary>
        public (long MemoryUsage, int ItemCount, long TotalReads, long TotalWrites, long TotalEvictions) GetStatistics()
        {
            return (
                _currentMemoryUsage,
                _storage.Count,
                _totalReads,
                _totalWrites,
                _totalEvictions
            );
        }

        /// <summary>
        /// Clear all data from RAMDisk.
        /// </summary>
        public Task ClearAsync()
        {
            _storage.Clear();
            _accessTracking.Clear();
            Interlocked.Exchange(ref _currentMemoryUsage, 0);
            _context?.LogWarning($"[RAMDisk] Cleared all data");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Dispose resources.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _autoSaveTimer?.Dispose();

            // Save to disk on dispose if configured
            if (!string.IsNullOrEmpty(_persistencePath))
            {
                SaveToDiskAsync().GetAwaiter().GetResult();
            }

            _isDisposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
