using DataWarehouse.SDK.Contracts;
using DataWarehouse.SDK.Primitives;
using System.Collections.Concurrent;

namespace DataWarehouse.Kernel.Indexing
{
    /// <summary>
    /// Minimal implementation for Laptop Mode
    /// </summary>
    public class InMemoryMetadataIndex : IMetadataIndex, IPlugin
    {
        /// <summary>
        /// ID
        /// </summary>
        public string Id => "builtin-memory-index";

        /// <summary>
        /// Name
        /// </summary>
        public string Name => "InMemory Metadata Index";

        /// <summary>
        /// Version
        /// </summary>
        public string Version => "1.0";
        private readonly ConcurrentDictionary<string, Manifest> _store = new();

        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="context"></param>
        public void Initialize(IKernelContext context) { }

        /// <summary>
        /// Index manifest
        /// </summary>
        /// <param name="manifest"></param>
        /// <returns></returns>
        public Task IndexManifestAsync(Manifest manifest)
        {
            _store[manifest.Id] = manifest;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Get manifest
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public Task<Manifest?> GetManifestAsync(string id)
        {
            _store.TryGetValue(id, out var m);
            return Task.FromResult(m);
        }

        /// <summary>
        /// Search
        /// Simple linear search for Laptop Mode
        /// </summary>
        /// <param name="query"></param>
        /// <param name="vector"></param>
        /// <param name="limit"></param>
        /// <returns></returns>
        public Task<string[]> SearchAsync(string query, float[]? vector, int limit)
        {
            // TODO: Implement basic tag search or return all for now
            return Task.FromResult(_store.Keys.Take(limit).ToArray());
        }

        /// <summary>
        /// Enumerate all
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        public IAsyncEnumerable<Manifest> EnumerateAllAsync(CancellationToken ct = default)
        {
            // Convert concurrent values to async stream
            return _store.Values.ToAsyncEnumerable();
        }

        /// <summary>
        /// Update last access
        /// </summary>
        /// <param name="id"></param>
        /// <param name="timestamp"></param>
        /// <returns></returns>
        public Task UpdateLastAccessAsync(string id, long timestamp)
        {
            if (_store.TryGetValue(id, out var manifest))
            {
                manifest.LastAccessedAt = timestamp;
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Execute Query
        /// </summary>
        /// <param name="query"></param>
        /// <param name="limit"></param>
        /// <returns></returns>
        public Task<string[]> ExecuteQueryAsync(string query, int limit)
        {
            // Simple mock implementation for in-memory
            // Supports naive "SELECT * FROM Manifests"
            return Task.FromResult(_store.Keys.Take(limit).ToArray());
        }

        /// <summary>
        /// Execute query
        /// </summary>
        /// <param name="query"></param>
        /// <param name="limit"></param>
        /// <returns></returns>
        public Task<string[]> ExecuteQueryAsync(CompositeQuery query, int limit = 50)
        {
            var results = _store.Values.AsEnumerable();

            foreach (var filter in query.Filters)
            {
                results = results.Where(m => ApplyFilter(m, filter));
            }

            var ids = results.Take(limit).Select(m => m.Id).ToArray();
            return Task.FromResult(ids);
        }

        private bool ApplyFilter(Manifest m, QueryFilter filter)
        {
            // Manual Property Mapping for Performance/Safety
            object? propValue = filter.Field switch
            {
                "Checksum" => m.Checksum,
                "OwnerId" => m.OwnerId,
                "ContainerId" => m.ContainerId,
                "SizeBytes" => m.SizeBytes,
                "BlobUri" => m.BlobUri,
                _ => null
            };

            if (propValue == null) return false;

            // Simple Equality Check
            string valStr = propValue.ToString() ?? "";
            string filterStr = filter.Value?.ToString() ?? "";

            return filter.Operator switch
            {
                "==" => valStr.Equals(filterStr, StringComparison.OrdinalIgnoreCase),
                "!=" => !valStr.Equals(filterStr, StringComparison.OrdinalIgnoreCase),
                "CONTAINS" => valStr.Contains(filterStr, StringComparison.OrdinalIgnoreCase),
                _ => false
            };
        }
    }
}