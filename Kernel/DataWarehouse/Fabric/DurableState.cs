using System.Collections.Concurrent;

namespace DataWarehouse.Fabric
{
    /// <summary>
    /// Lightweight, crash-consistent KV store for Fabric Metadata.
    /// Uses In-Memory speed + WAL durability.
    /// </summary>
    public class DurableState<T> : IDisposable
    {
        private readonly ConcurrentDictionary<string, T> _state = new();
        private readonly string _walPath;
        private readonly string _snapshotPath;
        private readonly object _lock = new object();
        private bool _isDirty;

        /// <summary>
        /// Check and reconnect on startup
        /// </summary>
        /// <param name="rootPath"></param>
        /// <param name="name"></param>
        public DurableState(string rootPath, string name)
        {
            _walPath = Path.Combine(rootPath, $"{name}.wal");
            _snapshotPath = Path.Combine(rootPath, $"{name}.state");

            // On startup, recover state
            Recover();
        }

        /// <summary>
        /// Check if connected
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool TryGet(string key, out T value) => _state.TryGetValue(key, out value!);

        /// <summary>
        /// Set state
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void Set(string key, T value)
        {
            _state[key] = value;

            // Immediate WAL persistence (Crash Consistency)
            // In high-perf server mode, we might batch this. For safety, we append immediately.
            lock (_lock)
            {
                var json = System.Text.Json.JsonSerializer.Serialize(value);
                File.AppendAllText(_walPath, $"SET|{key}|{json}{Environment.NewLine}");
                _isDirty = true;
            }
        }

        private void Recover()
        {
            // 1. Load Snapshot
            if (File.Exists(_snapshotPath))
            {
                var json = File.ReadAllText(_snapshotPath);
                var loaded = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, T>>(json);
                if (loaded != null)
                {
                    foreach (var kv in loaded) _state[kv.Key] = kv.Value;
                }
            }

            // 2. Replay WAL
            if (File.Exists(_walPath))
            {
                foreach (var line in File.ReadLines(_walPath))
                {
                    var parts = line.Split('|', 3);
                    if (parts.Length == 3 && parts[0] == "SET")
                    {
                        var val = System.Text.Json.JsonSerializer.Deserialize<T>(parts[2]);
                        if (val != null) _state[parts[1]] = val;
                    }
                }
            }
        }

        /// <summary>
        /// Get snapshot
        /// </summary>
        public void Snapshot()
        {
            if (!_isDirty) return;

            lock (_lock)
            {
                var temp = _snapshotPath + ".tmp";
                var json = System.Text.Json.JsonSerializer.Serialize(_state);
                File.WriteAllText(temp, json);
                File.Move(temp, _snapshotPath, overwrite: true);
                File.WriteAllText(_walPath, ""); // Truncate WAL
                _isDirty = false;
            }
        }

        /// <summary>
        /// Remove key
        /// </summary>
        /// <param name="key"></param>
        public void Remove(string key)
        {
            if (_state.TryRemove(key, out _))
            {
                lock (_lock)
                {
                    // Append DELETE command to WAL
                    File.AppendAllText(_walPath, $"DEL|{key}{Environment.NewLine}");
                    _isDirty = true;
                }
            }
        }

        /// <summary>
        /// Get all keys
        /// </summary>
        /// <returns></returns>
        public IEnumerable<KeyValuePair<string, T>> GetAll()
        {
            return _state.ToArray();
        }

        /// <summary>
        /// Safely dispose
        /// </summary>
        public void Dispose() => Snapshot();
    }
}