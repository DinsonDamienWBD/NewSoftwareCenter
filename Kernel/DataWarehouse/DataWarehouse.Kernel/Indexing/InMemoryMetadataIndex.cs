using DataWarehouse.SDK.Contracts;
using DataWarehouse.SDK.Primitives;
using System.Collections.Concurrent;
using System.Text.Json;

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
        public static string Id => "builtin-memory-index";

        /// <summary>
        /// Name
        /// </summary>
        public static string Name => "InMemory Metadata Index";

        /// <summary>
        /// Version
        /// </summary>
        public static string Version => "1.0";
        private readonly ConcurrentDictionary<string, Manifest> _store = new();

        /// <summary>
        /// Handshake implementation for IPlugin
        /// </summary>
        public Task<HandshakeResponse> OnHandshakeAsync(HandshakeRequest request)
        {
            return Task.FromResult(HandshakeResponse.Success(
                pluginId: Id,
                name: Name,
                version: new Version(Version),
                category: PluginCategory.Metadata,
                capabilities: new List<PluginCapabilityDescriptor>
                {
                    new PluginCapabilityDescriptor
                    {
                        CapabilityId = "indexing.memory.metadata",
                        DisplayName = "In-Memory Metadata Index",
                        Description = "Provides in-memory metadata indexing for laptop mode",
                        Category = CapabilityCategory.Metadata
                    }
                },
                initDuration: TimeSpan.Zero
            ));
        }

        /// <summary>
        /// Message handler (optional)
        /// </summary>
        public Task OnMessageAsync(PluginMessage message)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="context"></param>
        public static void Initialize(IKernelContext context) { }

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
            // Filter Manifests
            var matches = _store.Values.Where(m => Match(m, query));

            // Select results
            var results = matches.Take(limit).Select(m => JsonSerializer.Serialize(m)).ToArray();
            return Task.FromResult(results);
        }

        private static bool Match(Manifest manifest, CompositeQuery query)
        {
            // AND Logic: All filters must pass
            foreach (var filter in query.Filters)
            {
                if (!CheckCondition(manifest, filter)) return false;
            }
            return true;
        }

        private static bool CheckCondition(Manifest manifest, QueryFilter filter)
        {
            // 1. Resolve Value from Manifest
            // Check Top-Level Properties first
            object? actualValue = null;

            if (filter.Field.Equals("Id", StringComparison.OrdinalIgnoreCase)) actualValue = manifest.Id;
            else if (filter.Field.Equals("ContainerId", StringComparison.OrdinalIgnoreCase)) actualValue = manifest.ContainerId;
            else if (filter.Field.Equals("OwnerId", StringComparison.OrdinalIgnoreCase)) actualValue = manifest.OwnerId;
            else if (filter.Field.Equals("SizeBytes", StringComparison.OrdinalIgnoreCase)) actualValue = manifest.SizeBytes;
            else
            {
                // Check inside JsonData
                try
                {
                    using var doc = JsonDocument.Parse(JsonSerializer.Serialize(manifest));
                    if (doc.RootElement.TryGetProperty(filter.Field, out var prop))
                    {
                        actualValue = prop.ToString();
                    }
                }
                catch { return false; } // Invalid JSON or Prop missing
            }

            if (actualValue == null) return false;

            // 2. Compare
            string sActual = actualValue.ToString() ?? "";
            string sTarget = filter.Value?.ToString() ?? "";

            return filter.Operator switch
            {
                "==" => sActual.Equals(sTarget, StringComparison.OrdinalIgnoreCase),
                "!=" => !sActual.Equals(sTarget, StringComparison.OrdinalIgnoreCase),
                "contains" => sActual.Contains(sTarget, StringComparison.OrdinalIgnoreCase),
                ">" => long.TryParse(sActual, out long n1) && long.TryParse(sTarget, out long n2) && n1 > n2,
                "<" => long.TryParse(sActual, out long n1) && long.TryParse(sTarget, out long n2) && n1 < n2,
                _ => false
            };
        }

        private static bool ApplyFilter(Manifest m, QueryFilter filter)
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