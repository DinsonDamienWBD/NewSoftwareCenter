using DataWarehouse.Contracts;
using DataWarehouse.Primitives;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace DataWarehouse.Engine
{
    /// <summary>
    /// The brain
    /// A concrete implementation of IMetadataIndex so the system can actually run.
    /// </summary>
    public class InMemoryMetadataIndex : IQueryableIndex
    {
        /// <summary>
        /// ID
        /// </summary>
        public string Id => "MemIndex";

        /// <summary>
        /// Version
        /// </summary>
        public string Version => "1.0";

        private readonly ConcurrentDictionary<string, Manifest> _index = new();

        /// <summary>
        /// Index the manifest
        /// </summary>
        /// <param name="manifest"></param>
        /// <returns></returns>
        public Task IndexManifestAsync(Manifest manifest)
        {
            _index[manifest.Id] = manifest;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Search
        /// </summary>
        /// <param name="query"></param>
        /// <param name="vector"></param>
        /// <param name="limit"></param>
        /// <returns></returns>
        public Task<string[]> SearchAsync(string query, float[]? vector, int limit)
        {
            // Naive Linear Search (Production would use Lucene/VectorDB)
            var q = query.ToLowerInvariant();
            var results = _index.Values
                .Where(m => (m.ContentSummary?.ToLowerInvariant().Contains(q, StringComparison.InvariantCultureIgnoreCase) ?? false) ||
                            m.Tags.Keys.Any(k => k.Contains(q, StringComparison.InvariantCultureIgnoreCase)))
                .Take(limit)
                .Select(m => m.Id)
                .ToArray();

            return Task.FromResult(results);
        }

        // --- QUERY ENGINE (LINQ) ---

        /// <summary>
        /// Execute query
        /// </summary>
        /// <param name="query"></param>
        /// <param name="limit"></param>
        /// <returns></returns>
        public Task<string[]> ExecuteQueryAsync(CompositeQuery query, int limit = 50)
        {
            var results = _index.Values.AsEnumerable();

            if (query.Logic == "OR")
            {
                results = results.Where(m => query.Filters.Any(f => Matches(m, f)));
            }
            else // AND
            {
                results = results.Where(m => query.Filters.All(f => Matches(m, f)));
            }

            return Task.FromResult(results.Take(limit).Select(m => m.Id).ToArray());
        }

        /// <summary>
        /// Execute SQL
        /// </summary>
        /// <param name="sqlQuery"></param>
        /// <param name="limit"></param>
        /// <returns></returns>
        public Task<string[]> ExecuteSqlAsync(string sqlQuery, int limit = 50)
        {
            // Simple Parser: "Field Op Value"
            // Example: "SizeBytes > 1000"
            // NOTE: A real parser is complex. For InMemory, we recommend using CompositeQuery.
            // This is a minimal implementation for demo purposes.
            try
            {
                var parts = sqlQuery.Split(' ');
                if (parts.Length >= 3)
                {
                    var filter = new QueryFilter
                    {
                        Field = parts[0],
                        Operator = parts[1],
                        Value = parts[2]
                    };
                    return ExecuteQueryAsync(new CompositeQuery { Filters = { filter } }, limit);
                }
            }
            catch { /* Ignore parse errors */ }

            return Task.FromResult(Array.Empty<string>());
        }

        private static bool Matches(Manifest m, QueryFilter f)
        {
            // 1. Get Value via Reflection / Property Access
            object? actualVal = GetValue(m, f.Field);
            if (actualVal == null) return false;

            // 2. Compare
            string sAct = actualVal.ToString()!;
            string sTarget = f.Value.ToString()!;

            return f.Operator switch
            {
                "==" => sAct.Equals(sTarget, StringComparison.OrdinalIgnoreCase),
                "!=" => !sAct.Equals(sTarget, StringComparison.OrdinalIgnoreCase),
                "CONTAINS" => sAct.Contains(sTarget, StringComparison.OrdinalIgnoreCase),
                ">" => double.TryParse(sAct, out double d1) && double.TryParse(sTarget, out double d2) && d1 > d2,
                "<" => double.TryParse(sAct, out double d1) && double.TryParse(sTarget, out double d2) && d1 < d2,
                _ => false
            };
        }

        private static object? GetValue(Manifest m, string field)
        {
            if (field.Equals("SizeBytes", StringComparison.OrdinalIgnoreCase)) return m.SizeBytes;
            if (field.Equals("BlobUri", StringComparison.OrdinalIgnoreCase)) return m.BlobUri;
            if (field.StartsWith("Tags.", StringComparison.OrdinalIgnoreCase))
            {
                var key = field[5..];
                return m.Tags.TryGetValue(key, out var val) ? val : null;
            }
            return null;
        }

        /// <summary>
        /// We use ToArray() to avoid "Collection Modified" errors while iterating
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        public async IAsyncEnumerable<Manifest> EnumerateAllAsync([EnumeratorCancellation] CancellationToken ct = default)
        {
            foreach (var item in _index.Values)
            {
                // [FIX] Check for cancellation
                if (ct.IsCancellationRequested) yield break;

                yield return item;
                await Task.Yield(); // Async shim to ensure UI/Thread responsiveness
            }
        }

        /// <summary>
        /// Track last access
        /// </summary>
        /// <param name="id"></param>
        /// <param name="timestamp"></param>
        /// <returns></returns>
        public Task UpdateLastAccessAsync(string id, long timestamp)
        {
            if (_index.TryGetValue(id, out var manifest))
            {
                manifest.LastAccessedAt = timestamp;
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Get manifest
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public Task<Manifest?> GetManifestAsync(string id)
        {
            _index.TryGetValue(id, out var manifest);
            return Task.FromResult(manifest);
        }
    }
}