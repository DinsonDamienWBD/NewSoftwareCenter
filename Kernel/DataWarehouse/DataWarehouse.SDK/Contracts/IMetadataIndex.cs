using DataWarehouse.SDK.Primitives;

namespace DataWarehouse.SDK.Contracts
{
    /// <summary>
    /// Search Provider for Semantic Memory
    /// </summary>
    public interface IMetadataIndex : IPlugin
    {
        /// <summary>
        /// Index the Manifest 
        /// </summary>
        /// <param name="manifest"></param>
        /// <returns></returns>
        Task IndexManifestAsync(Primitives.Manifest manifest);

        /// <summary>
        /// Search
        /// </summary>
        /// <param name="query"></param>
        /// <param name="vector"></param>
        /// <param name="limit"></param>
        /// <returns></returns>
        Task<string[]> SearchAsync(string query, float[]? vector, int limit);

        /// <summary>
        /// Required for DataVacuum and Vector Cache Rehydration
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        IAsyncEnumerable<Manifest> EnumerateAllAsync(CancellationToken ct = default);

        /// <summary>
        /// Update last access
        /// </summary>
        /// <param name="id"></param>
        /// <param name="timestamp"></param>
        /// <returns></returns>
        Task UpdateLastAccessAsync(string id, long timestamp);

        /// <summary>
        /// Get manifest
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        Task<Primitives.Manifest?> GetManifestAsync(string id);

        /// <summary>
        /// For SQL
        /// </summary>
        /// <param name="query"></param>
        /// <param name="limit"></param>
        /// <returns></returns>
        Task<string[]> ExecuteQueryAsync(string query, int limit);

        /// <summary>
        /// Execute query
        /// </summary>
        /// <param name="query"></param>
        /// <param name="limit"></param>
        /// <returns></returns>
        Task<string[]> ExecuteQueryAsync(CompositeQuery query, int limit = 50);
    }
}
