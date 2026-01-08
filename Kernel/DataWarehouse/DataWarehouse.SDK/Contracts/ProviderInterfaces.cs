    namespace DataWarehouse.SDK.Contracts
{
    /// <summary>
    /// For active features (SQL Listener, Raft Consensus, Governance).
    /// The Kernel manages the lifecycle (Start/Stop) of these plugins.
    /// </summary>
    public interface IFeaturePlugin : IPlugin
    {
        /// <summary>
        /// Start
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        Task StartAsync(CancellationToken ct);

        /// <summary>
        /// Stop
        /// </summary>
        /// <returns></returns>
        Task StopAsync();
    }

    /// <summary>
    /// For V4 Hyperscale Storage (Segmented Disk, S3, Azure).
    /// </summary>
    public interface IStorageProvider : IPlugin
    {
        /// <summary>
        /// Scheme
        /// // "file", "s3", "azure"
        /// </summary>
        string Scheme { get; } 

        /// <summary>
        /// Save
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        Task SaveAsync(Uri uri, Stream data);

        /// <summary>
        /// Load
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        Task<Stream> LoadAsync(Uri uri);

        /// <summary>
        /// Delete
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        Task DeleteAsync(Uri uri);

        /// <summary>
        /// Exists
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        Task<bool> ExistsAsync(Uri uri);
    }
}