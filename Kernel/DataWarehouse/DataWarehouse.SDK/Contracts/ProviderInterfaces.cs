    namespace DataWarehouse.SDK.Contracts
{
    /// <summary>
    /// For active features (SQL Listener, Raft Consensus, Governance).
    /// The Kernel manages the lifecycle (Start/Stop) of these plugins.
    /// </summary>
    [Obsolete("Use FeaturePluginBase abstract class instead. Interfaces force code duplication. " +
              "FeaturePluginBase provides complete implementation - plugins only override InitializeFeatureAsync(), StartFeatureAsync(), StopFeatureAsync(). " +
              "See RULES.md Section 6 for CategoryBase pattern.", error: false)]
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
    [Obsolete("Use StorageProviderBase abstract class instead. Interfaces force code duplication. " +
              "StorageProviderBase provides complete CRUD implementation - plugins only override MountInternalAsync(), ReadBytesAsync(), WriteBytesAsync(), etc. " +
              "Reduces plugin code by 80%. See RULES.md Section 6 for CategoryBase pattern.", error: false)]
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
    [Obsolete("Use StorageProviderBase abstract class instead. This marker interface is redundant. " +
              "StorageProviderBase already provides both storage operations and feature lifecycle management. " +
              "See RULES.md Section 6 for CategoryBase pattern.", error: false)]
    public interface IStoragePlugin : IStorageProvider, IFeaturePlugin
    {
        // Marker interface for enhanced storage plugins
        // Inherits both storage operations and feature lifecycle management
    }

    /// <summary>
    /// Interface for network/protocol interface plugins (REST, gRPC, SQL, etc.).
    /// Provides external access to DataWarehouse through various protocols.
    /// </summary>
    [Obsolete("Use InterfacePluginBase abstract class instead. Interfaces force code duplication. " +
              "InterfacePluginBase provides complete implementation - plugins only override InitializeInterfaceAsync(), StartListeningAsync(), StopListeningAsync(). " +
              "See RULES.md Section 6 for CategoryBase pattern.", error: false)]
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