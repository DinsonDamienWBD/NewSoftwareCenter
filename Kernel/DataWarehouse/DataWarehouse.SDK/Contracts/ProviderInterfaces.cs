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

    /// <summary>
    /// Extended storage plugin interface with enhanced metadata and capabilities.
    /// Provides additional plugin-specific functionality beyond basic IStorageProvider.
    /// </summary>
    public interface IStoragePlugin : IStorageProvider, IFeaturePlugin
    {
        // Marker interface for enhanced storage plugins
        // Inherits both storage operations and feature lifecycle management
    }

    /// <summary>
    /// Interface for network/protocol interface plugins (REST, gRPC, SQL, etc.).
    /// Provides external access to DataWarehouse through various protocols.
    /// </summary>
    public interface IInterfacePlugin : IFeaturePlugin
    {
        /// <summary>
        /// The protocol name this interface provides (e.g., "REST", "gRPC", "SQL").
        /// </summary>
        string Protocol { get; }

        /// <summary>
        /// The port or endpoint this interface listens on.
        /// </summary>
        string Endpoint { get; }

        /// <summary>
        /// Check if the interface is currently accepting connections.
        /// </summary>
        bool IsListening { get; }
    }
}