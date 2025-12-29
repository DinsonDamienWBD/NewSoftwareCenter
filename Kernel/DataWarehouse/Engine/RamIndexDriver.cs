using Core.Contracts;
using Core.Data;
using System.Collections.Concurrent;

namespace DataWarehouse.Engine
{
    /// <summary>
    /// A high-performance, in-memory index for O(1) lookups and fast searching.
    /// Persists state to disk (Snapshots) to survive restarts.
    /// </summary>
    public class RamIndexDriver(string rootPath, ISerializer serializer) : IIndexDriver
    {
        // The "RAM Disk": Thread-safe in-memory store
        // Key: EntityId, Value: Metadata Dictionary
        private readonly ConcurrentDictionary<string, Dictionary<string, string>> _index = new();

        private readonly string _snapshotPath = Path.Combine(rootPath, "system.index");
        private readonly ISerializer _serializer = serializer;
        private readonly object _lock = new();

        /// <summary>
        /// Upsert entry
        /// </summary>
        /// <param name="id"></param>
        /// <param name="metadata"></param>
        /// <returns></returns>
        public Task UpsertEntryAsync(string id, Dictionary<string, string> metadata)
        {
            // O(1) Operation
            _index[id] = metadata;

            // In a real high-scale app, we would debounce this save or use a WAL (Write Ahead Log).
            // For now, we save async to ensure durability.
            return SaveSnapshotAsync();
        }

        /// <summary>
        /// Delete an entry
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public Task RemoveEntryAsync(string id)
        {
            _index.TryRemove(id, out _);
            return SaveSnapshotAsync();
        }

        /// <summary>
        /// Search for an entry
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        public Task<IEnumerable<string>> SearchAsync(string query)
        {
            // Linear scan of RAM is incredibly fast for < 100k items.
            // For > 1M items, we would plug in Lucene/SQLite here without changing the interface.

            if (string.IsNullOrWhiteSpace(query))
                return Task.FromResult(_index.Keys.AsEnumerable());

            var q = query.ToLowerInvariant();

            var results = _index
                .Where(kvp =>
                    kvp.Key.Contains(q, StringComparison.InvariantCultureIgnoreCase) || // Search ID
                    kvp.Value.Values.Any(v => v != null && v.Contains(q, StringComparison.InvariantCultureIgnoreCase)) // Search Values
                )
                .Select(kvp => kvp.Key);

            return Task.FromResult(results);
        }

        /// <summary>
        /// Rebuild the index
        /// </summary>
        /// <returns></returns>
        public async Task RebuildAsync()
        {
            // Load from disk
            if (File.Exists(_snapshotPath))
            {
                using var fs = new FileStream(_snapshotPath, FileMode.Open, FileAccess.Read);
                var loaded = await _serializer.DeserializeAsync<Dictionary<string, Dictionary<string, string>>>(fs);

                if (loaded != null)
                {
                    _index.Clear();
                    foreach (var kvp in loaded)
                    {
                        _index[kvp.Key] = kvp.Value;
                    }
                }
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
    }
}