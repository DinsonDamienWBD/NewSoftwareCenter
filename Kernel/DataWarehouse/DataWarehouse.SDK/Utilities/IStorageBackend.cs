namespace DataWarehouse.SDK.Utilities
{
    /// <summary>
    /// Lightweight storage backend interface for internal utilities.
    ///
    /// This is NOT a plugin interface - it's a simple abstraction for storage operations
    /// used by internal components like DurableStateV2, FeatureManager, etc.
    ///
    /// For full plugin storage providers, use StorageProviderBase instead.
    /// </summary>
    public interface IStorageBackend
    {
        /// <summary>
        /// Storage scheme identifier (e.g., "file", "memory", "s3").
        /// Used to construct URIs for storage operations.
        /// </summary>
        string Scheme { get; }

        /// <summary>
        /// Save data to storage asynchronously.
        /// </summary>
        /// <param name="uri">Storage URI (e.g., "file://path/to/file")</param>
        /// <param name="data">Data stream to save</param>
        Task SaveAsync(Uri uri, Stream data);

        /// <summary>
        /// Load data from storage asynchronously.
        /// </summary>
        /// <param name="uri">Storage URI</param>
        /// <returns>Stream containing stored data</returns>
        /// <exception cref="FileNotFoundException">If resource doesn't exist</exception>
        Task<Stream> LoadAsync(Uri uri);

        /// <summary>
        /// Delete data from storage asynchronously.
        /// </summary>
        /// <param name="uri">Storage URI</param>
        Task DeleteAsync(Uri uri);

        /// <summary>
        /// Check if data exists in storage asynchronously.
        /// </summary>
        /// <param name="uri">Storage URI</param>
        /// <returns>True if resource exists, false otherwise</returns>
        Task<bool> ExistsAsync(Uri uri);
    }
}
