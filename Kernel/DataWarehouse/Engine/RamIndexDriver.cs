using Core.Contracts;
using Core.Data;
using System.Collections.Concurrent;
using System.Timers;
using SysTimer = System.Timers.Timer;

namespace DataWarehouse.Engine
{
    /// <summary>
    /// A high-performance, in-memory index for O(1) lookups and fast searching.
    /// Persists state to disk (Snapshots) to survive restarts.
    /// </summary>
    public class RamIndexDriver : IIndexDriver, IDisposable
    {
        // The "RAM Disk": Thread-safe in-memory store
        // Key: EntityId, Value: Metadata Dictionary
        private readonly ConcurrentDictionary<string, Dictionary<string, string>> _index = new();

        private readonly string _snapshotPath;
        private readonly string _walPath;
        private readonly ISerializer _serializer;

        // Debouncing
        private readonly SysTimer _saveTimer;
        private bool _isDirty;
        private readonly object _lock = new();

        // NEW: Error Tracking
        private Exception? _lastSaveError;

        /// <summary>
        /// Last error
        /// </summary>
        public Exception? LastError => _lastSaveError;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="rootPath"></param>
        /// <param name="serializer"></param>
        public RamIndexDriver(string rootPath,
                              ISerializer serializer)
        {
            _snapshotPath = Path.Combine(rootPath, "system.index");
            _walPath = Path.Combine(rootPath, "index.wal");
            _serializer = serializer;

            // Save every 5 seconds if dirty
            _saveTimer = new SysTimer(5000);
            _saveTimer.Elapsed += async (s, e) => await FlushSnapshotAsync();
            _saveTimer.AutoReset = true;
            _saveTimer.Start();
        }

        /// <summary>
        /// Upsert entry
        /// </summary>
        /// <param name="id"></param>
        /// <param name="metadata"></param>
        /// <returns></returns>
        public async Task UpsertEntryAsync(string id, Dictionary<string, string> metadata)
        {
            // O(1) Operation
            _index[id] = metadata;

            // 1. Write to WAL (Immediate Durability)
            var logEntry = $"UPSERT|{id}|{_serializer.Serialize(metadata)}{Environment.NewLine}";
            await File.AppendAllTextAsync(_walPath, logEntry);

            // 2. Mark for snapshot
            _isDirty = true;
        }

        /// <summary>
        /// Delete an entry
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task RemoveEntryAsync(string id)
        {
            _index.TryRemove(id, out _);

            // 1. Write to WAL
            var logEntry = $"REMOVE|{id}{Environment.NewLine}";
            await File.AppendAllTextAsync(_walPath, logEntry);

            _isDirty = true;
        }

        /// <summary>
        /// Search for an entry
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        public Task<IEnumerable<string>> SearchAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return Task.FromResult(_index.Keys.AsEnumerable());

            var q = query.ToLowerInvariant();
            var results = _index
                .Where(kvp => kvp.Key.Contains(q, StringComparison.InvariantCultureIgnoreCase) || kvp.Value.Values.Any(v => v != null && v.Contains(q, StringComparison.InvariantCultureIgnoreCase)))
                .Select(kvp => kvp.Key);

            return Task.FromResult(results);
        }

        /// <summary>
        /// Rebuild the index
        /// </summary>
        /// <returns></returns>
        public async Task RebuildAsync()
        {
            // 1. Load Snapshot
            if (File.Exists(_snapshotPath))
            {
                using var fs = new FileStream(_snapshotPath, FileMode.Open, FileAccess.Read);
                var loaded = await _serializer.DeserializeAsync<Dictionary<string, Dictionary<string, string>>>(fs);
                if (loaded != null)
                {
                    foreach (var kvp in loaded) _index[kvp.Key] = kvp.Value;
                }
            }

            // 2. Replay WAL (Crash Recovery)
            if (File.Exists(_walPath))
            {
                var lines = await File.ReadAllLinesAsync(_walPath);
                foreach (var line in lines)
                {
                    var parts = line.Split('|', 3);
                    if (parts[0] == "UPSERT" && parts.Length == 3)
                    {
                        var data = _serializer.Deserialize<Dictionary<string, string>>(parts[2]);
                        if (data != null) _index[parts[1]] = data;
                    }
                    else if (parts[0] == "REMOVE" && parts.Length >= 2)
                    {
                        _index.TryRemove(parts[1], out _);
                    }
                }
            }
        }

        private async Task FlushSnapshotAsync()
        {
            if (!_isDirty) return;

            try
            {
                var temp = _snapshotPath + ".tmp";
                using (var fs = new FileStream(temp, FileMode.Create, FileAccess.Write))
                {
                    var snapshot = new Dictionary<string, Dictionary<string, string>>(_index);
                    await _serializer.SerializeAsync(fs, snapshot);
                }

                File.Move(temp, _snapshotPath, overwrite: true);
                File.WriteAllText(_walPath, string.Empty);
                _isDirty = false;

                // Clear error if success
                _lastSaveError = null;
            }
            catch (Exception ex)
            {
                // CAPTURE ERROR for Health Probe
                _lastSaveError = ex;
            }
        }

        /// <summary>
        /// Save a snapshot of the index to disk.
        /// </summary>
        /// <returns></returns>
        private async Task SaveSnapshotAsync()
        {
            // Serialize to temp file then swap (Atomic Save)
            var temp = _snapshotPath + ".tmp";
            using (var fs = new FileStream(temp, FileMode.Create, FileAccess.Write))
            {
                // Snapshot current state
                var snapshot = new Dictionary<string, Dictionary<string, string>>(_index);
                await _serializer.SerializeAsync(fs, snapshot);
            }

            if (File.Exists(_snapshotPath)) File.Delete(_snapshotPath);
            File.Move(temp, _snapshotPath);
        }

        /// <summary>
        /// Safe disposal
        /// </summary>
        public void Dispose()
        {
            _saveTimer.Stop();
            FlushSnapshotAsync().Wait();
            _saveTimer.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}