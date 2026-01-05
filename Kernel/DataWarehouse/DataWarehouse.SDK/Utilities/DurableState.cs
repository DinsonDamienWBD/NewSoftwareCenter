using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace DataWarehouse.SDK.Utilities
{
    /// <summary>
    /// Durable state
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class DurableState<T>: IDisposable
    {
        private readonly string _filePath;
        private readonly ConcurrentDictionary<string, T> _cache = new();
        private readonly Lock _lock = new();
        private FileStream? _journalStream;
        private BinaryWriter? _writer;

        // Threshold to trigger compaction (e.g., 1000 operations)
        private int _opCount = 0;
        private const int CompactionThreshold = 5000;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="filePath"></param>
        public DurableState(string filePath)
        {
            _filePath = filePath;
            Load();
        }

        private void Load()
        {
            if (!File.Exists(_filePath))
            {
                InitializeJournal();
                return;
            }

            // Replay Log
            try
            {
                using var fs = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var reader = new BinaryReader(fs);

                while (fs.Position < fs.Length)
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
                            if (val != null) _cache[key] = val;
                        }
                        else if (opCode == 2) // Remove
                        {
                            _cache.TryRemove(key, out _);
                        }
                    }
                    catch (EndOfStreamException) { break; } // Safe truncation
                }
            }
            catch (Exception)
            {
                // Log corruption logic would go here. For now, we accept partial replay.
            }

            InitializeJournal();
        }

        private void InitializeJournal()
        {
            // Open for Appending
            _journalStream = new FileStream(_filePath, FileMode.Append, FileAccess.Write, FileShare.Read);
            _writer = new BinaryWriter(_journalStream, Encoding.UTF8, leaveOpen: true);
        }

        /// <summary>
        /// Set
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void Set(string key, T value)
        {
            lock (_lock)
            {
                _cache[key] = value;
                AppendLog(1, key, value);
                CheckCompaction();
            }
        }

        /// <summary>
        /// Remove
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool Remove(string key, out T? value)
        {
            lock (_lock)
            {
                if (_cache.TryRemove(key, out value))
                {
                    AppendLog(2, key, default);
                    CheckCompaction();
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Get
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public T? Get(string key) => _cache.TryGetValue(key, out var val) ? val : default;

        /// <summary>
        /// TryGet
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool TryGet(string key, out T? value) => _cache.TryGetValue(key, out value);

        /// <summary>
        /// GetAll
        /// </summary>
        /// <returns></returns>
        public IEnumerable<T> GetAll() => _cache.Values;

        /// <summary>
        /// GetAllKeyValues
        /// </summary>
        /// <returns></returns>
        public IEnumerable<KeyValuePair<string, T>> GetAllKeyValues() => _cache;

        private void AppendLog(byte opCode, string key, T? value)
        {
            if (_writer == null) return;

            // Format: [OpCode:1] [Key:Str] [Len:4] [Body:N]
            // Or:     [OpCode:1] [Key:Str] (For Remove)

            _writer.Write(opCode);
            _writer.Write(key);

            if (opCode == 1 && value != null)
            {
                var json = JsonSerializer.Serialize(value);
                var bytes = Encoding.UTF8.GetBytes(json);
                _writer.Write(bytes.Length);
                _writer.Write(bytes);
            }

            _writer.Flush();
            // _journalStream.Flush(true); // Uncomment for strict durability (slower)

            _opCount++;
        }

        private void CheckCompaction()
        {
            if (_opCount >= CompactionThreshold)
            {
                Compact();
                _opCount = 0;
            }
        }

        /// <summary>
        /// Rewrites the log to contain only the current state (Snapshot).
        /// Reduces file size and replay time.
        /// </summary>
        public void Compact()
        {
            string tempPath = _filePath + ".compact";
            using (var fs = new FileStream(tempPath, FileMode.Create))
            using (var writer = new BinaryWriter(fs))
            {
                foreach (var kvp in _cache)
                {
                    writer.Write((byte)1); // Set
                    writer.Write(kvp.Key);
                    var json = JsonSerializer.Serialize(kvp.Value);
                    var bytes = Encoding.UTF8.GetBytes(json);
                    writer.Write(bytes.Length);
                    writer.Write(bytes);
                }
                fs.Flush();
            }

            // Atomic Swap
            if (_writer != null)
            {
                _writer.Close();
                _journalStream?.Close();
            }

            File.Move(tempPath, _filePath, overwrite: true);

            // Re-open
            InitializeJournal();
        }

        /// <summary>
        /// Expose as Dictionary for complex LINQ if needed
        /// </summary>
        /// <returns></returns>
        public IDictionary<string, T> ToDictionary() => _cache;

        /// <summary>
        /// Safely dispose
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Safely Dispose
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            _writer?.Dispose();
            _journalStream?.Dispose();
        }
    }
}