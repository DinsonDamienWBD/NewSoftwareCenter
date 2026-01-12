using DataWarehouse.SDK.Primitives;
using DataWarehouse.SDK.Utilities;

namespace DataWarehouse.SDK.Contracts
{
    /// <summary>
    /// Abstract base class for all plugins. Provides default implementations
    /// of common plugin functionality. Plugins should inherit from this instead
    /// of implementing IPlugin directly.
    /// AI-native: Includes built-in support for AI-driven operations.
    /// </summary>
    public abstract class PluginBase : IPlugin
    {
        /// <summary>
        /// Unique Plugin ID - must be set by derived classes.
        /// </summary>
        public abstract Guid Id { get; }

        /// <summary>
        /// Gets the category of the plugin.
        /// </summary>
        public abstract PluginCategory Category { get; }

        /// <summary>
        /// Human-readable Name - must be set by derived classes.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Semantic Version - default implementation returns "1.0.0".
        /// </summary>
        public virtual string Version => "1.0.0";

        /// <summary>
        /// Default handshake implementation. Override to provide custom initialization.
        /// </summary>
        public virtual Task<HandshakeResponse> OnHandshakeAsync(HandshakeRequest request)
        {
            return Task.FromResult(new HandshakeResponse
            {
                PluginId = Id.ToString(),
                Name = Name,
                Version = Version.StartsWith("v") ? new Version(Version.Substring(1)) : new Version(Version),
                Category = Category,
                Success = true,
                ReadyState = PluginReadyState.Ready,
                Capabilities = GetCapabilities(),
                Dependencies = GetDependencies(),
                Metadata = GetMetadata()
            });
        }

        /// <summary>
        /// Default message handler. Override to handle custom messages.
        /// </summary>
        public virtual Task OnMessageAsync(PluginMessage message)
        {
            // Default: log and ignore
            return Task.CompletedTask;
        }

        /// <summary>
        /// Override to provide plugin capabilities for AI agents.
        /// </summary>
        protected virtual List<PluginCapabilityDescriptor> GetCapabilities()
        {
            return new List<PluginCapabilityDescriptor>();
        }

        /// <summary>
        /// Override to declare plugin dependencies.
        /// </summary>
        protected virtual List<PluginDependency> GetDependencies()
        {
            return new List<PluginDependency>();
        }

        /// <summary>
        /// Override to provide additional metadata for AI agents.
        /// </summary>
        protected virtual Dictionary<string, object> GetMetadata()
        {
            return new Dictionary<string, object>
            {
                ["Description"] = $"{Name} plugin for DataWarehouse",
                ["AIFriendly"] = true,
                ["SupportsStreaming"] = false
            };
        }
    }

    /// <summary>
    /// Abstract base class for data transformation plugins (Crypto, Compression, etc.).
    /// Provides default implementations for common transformation operations.
    /// AI-native: Supports intelligent transformation based on context.
    /// </summary>
    public abstract class DataTransformationPluginBase : PluginBase, IDataTransformation
    {
        /// <summary>
        /// Category is always DataTransformationProvider for transformation plugins.
        /// </summary>
        public override PluginCategory Category => PluginCategory.DataTransformationProvider;

        /// <summary>
        /// Sub-category for more specific classification (e.g., "Encryption", "Compression").
        /// Must be implemented by derived classes.
        /// </summary>
        public abstract string SubCategory { get; }

        /// <summary>
        /// Quality level (1-100) for sorting and selection.
        /// Default is 50 (balanced). Override for specific quality levels.
        /// </summary>
        public virtual int QualityLevel => 50;

        /// <summary>
        /// Transform data during write operations (e.g., encrypt, compress).
        /// Must be implemented by derived classes.
        /// </summary>
        public abstract Stream OnWrite(Stream input, IKernelContext context, Dictionary<string, object> args);

        /// <summary>
        /// Transform data during read operations (e.g., decrypt, decompress).
        /// Must be implemented by derived classes.
        /// </summary>
        public abstract Stream OnRead(Stream stored, IKernelContext context, Dictionary<string, object> args);

        protected override Dictionary<string, object> GetMetadata()
        {
            var metadata = base.GetMetadata();
            metadata["SubCategory"] = SubCategory;
            metadata["QualityLevel"] = QualityLevel;
            metadata["SupportsStreaming"] = true;
            metadata["TransformationType"] = "Bidirectional";
            return metadata;
        }
    }

    /// <summary>
    /// Abstract base class for storage provider plugins (Local, S3, IPFS, etc.).
    /// Provides default implementations for common storage operations.
    /// AI-native: Supports intelligent storage decisions based on content analysis.
    /// </summary>
    public abstract class StorageProviderPluginBase : PluginBase, IStorageProvider
    {
        /// <summary>
        /// Category is always StorageProvider for storage plugins.
        /// </summary>
        public override PluginCategory Category => PluginCategory.StorageProvider;

        /// <summary>
        /// URI scheme for this storage provider (e.g., "file", "s3", "ipfs").
        /// Must be implemented by derived classes.
        /// </summary>
        public abstract string Scheme { get; }

        /// <summary>
        /// Save data to storage. Must be implemented by derived classes.
        /// </summary>
        public abstract Task SaveAsync(Uri uri, Stream data);

        /// <summary>
        /// Retrieve data from storage. Must be implemented by derived classes.
        /// </summary>
        public abstract Task<Stream> LoadAsync(Uri uri);

        /// <summary>
        /// Delete data from storage. Must be implemented by derived classes.
        /// </summary>
        public abstract Task DeleteAsync(Uri uri);

        /// <summary>
        /// Check if data exists. Default implementation tries to load and catches exceptions.
        /// Override for more efficient existence checks.
        /// </summary>
        public virtual async Task<bool> ExistsAsync(Uri uri)
        {
            try
            {
                using var stream = await LoadAsync(uri);
                return stream != null;
            }
            catch
            {
                return false;
            }
        }

        protected override Dictionary<string, object> GetMetadata()
        {
            var metadata = base.GetMetadata();
            metadata["StorageType"] = Scheme;
            metadata["SupportsStreaming"] = true;
            metadata["SupportsConcurrency"] = false;
            return metadata;
        }
    }

    /// <summary>
    /// Abstract base class for metadata indexing plugins (SQLite, Postgres, etc.).
    /// Provides default implementations for common indexing operations.
    /// AI-native: Supports semantic search and vector embeddings.
    /// </summary>
    public abstract class MetadataIndexPluginBase : PluginBase, IMetadataIndex
    {
        /// <summary>
        /// Category is always MetadataIndexingProvider for indexing plugins.
        /// </summary>
        public override PluginCategory Category => PluginCategory.MetadataIndexingProvider;

        /// <summary>
        /// Index a manifest. Must be implemented by derived classes.
        /// </summary>
        public abstract Task IndexManifestAsync(Manifest manifest);

        /// <summary>
        /// Search for manifests with optional vector search.
        /// Must be implemented by derived classes.
        /// </summary>
        public abstract Task<string[]> SearchAsync(string query, float[]? vector, int limit);

        /// <summary>
        /// Enumerate all manifests. Must be implemented by derived classes.
        /// </summary>
        public abstract IAsyncEnumerable<Manifest> EnumerateAllAsync(CancellationToken ct = default);

        /// <summary>
        /// Update last access time. Must be implemented by derived classes.
        /// </summary>
        public abstract Task UpdateLastAccessAsync(string id, long timestamp);

        /// <summary>
        /// Get manifest by ID. Must be implemented by derived classes.
        /// </summary>
        public abstract Task<Manifest?> GetManifestAsync(string id);

        /// <summary>
        /// Execute text query. Must be implemented by derived classes.
        /// </summary>
        public abstract Task<string[]> ExecuteQueryAsync(string query, int limit);

        /// <summary>
        /// Execute composite query. Default implementation delegates to text query.
        /// Override for advanced query support.
        /// </summary>
        public virtual Task<string[]> ExecuteQueryAsync(CompositeQuery query, int limit = 50)
        {
            // Default: convert composite to text
            return ExecuteQueryAsync(query.ToString() ?? string.Empty, limit);
        }

        protected override Dictionary<string, object> GetMetadata()
        {
            var metadata = base.GetMetadata();
            metadata["IndexType"] = "Metadata";
            metadata["SupportsFullTextSearch"] = true;
            metadata["SupportsSemanticSearch"] = false;
            metadata["SupportsVectorSearch"] = false;
            metadata["SupportsCompositeQueries"] = false;
            return metadata;
        }
    }

    /// <summary>
    /// Abstract base class for feature plugins (SQL Listener, Consensus, Governance).
    /// Provides default implementations for lifecycle management.
    /// AI-native: Supports intelligent feature activation and monitoring.
    /// </summary>
    public abstract class FeaturePluginBase : PluginBase, IFeaturePlugin
    {
        /// <summary>
        /// Start the feature. Must be implemented by derived classes.
        /// </summary>
        public abstract Task StartAsync(CancellationToken ct);

        /// <summary>
        /// Stop the feature. Must be implemented by derived classes.
        /// </summary>
        public abstract Task StopAsync();

        protected override Dictionary<string, object> GetMetadata()
        {
            var metadata = base.GetMetadata();
            metadata["FeatureType"] = "Generic";
            metadata["RequiresLifecycleManagement"] = true;
            metadata["SupportsHotReload"] = false;
            return metadata;
        }
    }

    /// <summary>
    /// Abstract base class for security provider plugins (ACL, Encryption, etc.).
    /// Provides default implementations for common security operations.
    /// AI-native: Supports intelligent access control based on context.
    /// </summary>
    public abstract class SecurityProviderPluginBase : PluginBase
    {
        /// <summary>
        /// Category is always SecurityProvider for security plugins.
        /// </summary>
        public override PluginCategory Category => PluginCategory.SecurityProvider;

        protected override Dictionary<string, object> GetMetadata()
        {
            var metadata = base.GetMetadata();
            metadata["SecurityType"] = "Generic";
            metadata["SupportsEncryption"] = false;
            metadata["SupportsACL"] = false;
            metadata["SupportsAuthentication"] = false;
            return metadata;
        }
    }

    /// <summary>
    /// Abstract base class for orchestration provider plugins (Workflow, Pipeline, etc.).
    /// Provides default implementations for common orchestration operations.
    /// AI-native: Supports autonomous workflow creation and execution.
    /// </summary>
    public abstract class OrchestrationProviderPluginBase : PluginBase
    {
        /// <summary>
        /// Category is always OrchestrationProvider for orchestration plugins.
        /// </summary>
        public override PluginCategory Category => PluginCategory.OrchestrationProvider;

        protected override Dictionary<string, object> GetMetadata()
        {
            var metadata = base.GetMetadata();
            metadata["OrchestrationType"] = "Generic";
            metadata["SupportsWorkflows"] = false;
            metadata["SupportsPipelines"] = false;
            metadata["SupportsAIOrchestration"] = true;
            return metadata;
        }
    }
}
