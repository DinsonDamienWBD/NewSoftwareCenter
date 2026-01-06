using System;
using System.Collections.Generic;

namespace DataWarehouse.SDK.Contracts
{
    // ============================================================================
    // PLUGIN HANDSHAKE PROTOCOL
    // ============================================================================
    // Decouples plugin initialization from direct method calls.
    // Enables async initialization, parallel loading, and future remote plugins.
    // ============================================================================

    /// <summary>
    /// Sent by Kernel to plugin during initialization.
    /// Plugin responds asynchronously with HandshakeResponse.
    /// </summary>
    public class HandshakeRequest
    {
        /// <summary>
        /// Unique identifier for this kernel instance.
        /// </summary>
        public string KernelId { get; init; } = string.Empty;

        /// <summary>
        /// Protocol version for compatibility checking.
        /// Format: "Major.Minor" (e.g., "1.0")
        /// </summary>
        public string ProtocolVersion { get; init; } = "1.0";

        /// <summary>
        /// Timestamp when handshake was initiated.
        /// </summary>
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Operating mode (Laptop/Server/Hyperscale) for plugin optimization.
        /// </summary>
        public OperatingMode Mode { get; init; }

        /// <summary>
        /// Root path of the DataWarehouse instance.
        /// </summary>
        public string RootPath { get; init; } = string.Empty;

        /// <summary>
        /// List of plugins already loaded (for dependency resolution).
        /// </summary>
        public List<PluginDescriptor> AlreadyLoadedPlugins { get; init; } = new();
    }

    /// <summary>
    /// Minimal descriptor of a loaded plugin for dependency checking.
    /// </summary>
    public class PluginDescriptor
    {
        public string PluginId { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Version { get; init; } = string.Empty;
        public PluginCategory Category { get; init; }
        public List<string> Interfaces { get; init; } = new();
    }

    /// <summary>
    /// Plugin's response to handshake request.
    /// Includes identity, capabilities, dependencies, and readiness state.
    /// </summary>
    public class HandshakeResponse
    {
        // ===== IDENTITY =====

        /// <summary>
        /// Unique plugin identifier (e.g., "DataWarehouse.Compression.GZip").
        /// </summary>
        public string PluginId { get; init; } = string.Empty;

        /// <summary>
        /// Human-readable plugin name.
        /// </summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// Semantic version of the plugin.
        /// </summary>
        public Version Version { get; init; } = new Version(1, 0, 0);

        /// <summary>
        /// Broad category for organizational purposes.
        /// </summary>
        public PluginCategory Category { get; init; }

        // ===== READINESS =====

        /// <summary>
        /// Whether initialization succeeded.
        /// </summary>
        public bool Success { get; init; }

        /// <summary>
        /// Current operational state of the plugin.
        /// </summary>
        public PluginReadyState ReadyState { get; init; }

        /// <summary>
        /// Error message if initialization failed.
        /// </summary>
        public string? ErrorMessage { get; init; }

        // ===== CAPABILITIES =====

        /// <summary>
        /// List of capabilities this plugin provides.
        /// Used by AI and Kernel for dynamic discovery.
        /// </summary>
        public List<PluginCapabilityDescriptor> Capabilities { get; init; } = new();

        // ===== DEPENDENCIES =====

        /// <summary>
        /// Other plugins/interfaces this plugin requires.
        /// </summary>
        public List<PluginDependency> Dependencies { get; init; } = new();

        // ===== METADATA =====

        /// <summary>
        /// Custom metadata for specific plugins (model paths, config, etc.).
        /// </summary>
        public Dictionary<string, object> Metadata { get; init; } = new();

        /// <summary>
        /// How long initialization took (for performance monitoring).
        /// </summary>
        public TimeSpan InitializationDuration { get; init; }

        /// <summary>
        /// If set, Kernel will periodically send health check messages.
        /// </summary>
        public TimeSpan? HealthCheckInterval { get; init; }

        // ===== FACTORY METHODS =====

        public static HandshakeResponse Success(
            string pluginId,
            string name,
            Version version,
            PluginCategory category,
            List<PluginCapabilityDescriptor>? capabilities = null,
            TimeSpan? initDuration = null)
        {
            return new HandshakeResponse
            {
                Success = true,
                PluginId = pluginId,
                Name = name,
                Version = version,
                Category = category,
                ReadyState = PluginReadyState.Ready,
                Capabilities = capabilities ?? new(),
                InitializationDuration = initDuration ?? TimeSpan.Zero
            };
        }

        public static HandshakeResponse Failure(
            string pluginId,
            string name,
            string errorMessage)
        {
            return new HandshakeResponse
            {
                Success = false,
                PluginId = pluginId,
                Name = name,
                ReadyState = PluginReadyState.NotReady,
                ErrorMessage = errorMessage
            };
        }

        public static HandshakeResponse Initializing(
            string pluginId,
            string name,
            Version version,
            PluginCategory category,
            string statusMessage)
        {
            return new HandshakeResponse
            {
                Success = true,
                PluginId = pluginId,
                Name = name,
                Version = version,
                Category = category,
                ReadyState = PluginReadyState.Initializing,
                ErrorMessage = statusMessage
            };
        }
    }

    /// <summary>
    /// Broad categorization of plugins.
    /// </summary>
    public enum PluginCategory
    {
        /// <summary>
        /// Transforms data in-flight (compression, encryption, etc.).
        /// </summary>
        Pipeline,

        /// <summary>
        /// Physical storage backends (local, S3, IPFS, etc.).
        /// </summary>
        Storage,

        /// <summary>
        /// Metadata indexing and search (SQLite, PostgreSQL, etc.).
        /// </summary>
        Metadata,

        /// <summary>
        /// Security, ACL, key management.
        /// </summary>
        Security,

        /// <summary>
        /// Distributed coordination (Raft, consensus, etc.).
        /// </summary>
        Orchestration,

        /// <summary>
        /// AI-driven governance and intelligence.
        /// </summary>
        Intelligence,

        /// <summary>
        /// Protocol adapters (SQL listener, GraphQL, etc.).
        /// </summary>
        Interface,

        /// <summary>
        /// Advanced features (tiering, deduplication, etc.).
        /// </summary>
        Feature
    }

    /// <summary>
    /// Operational readiness state of a plugin.
    /// </summary>
    public enum PluginReadyState
    {
        /// <summary>
        /// Initialization failed, plugin cannot be used.
        /// </summary>
        NotReady,

        /// <summary>
        /// Still initializing (e.g., loading AI models).
        /// Plugin can respond but may have limited capabilities.
        /// </summary>
        Initializing,

        /// <summary>
        /// Some capabilities available, others pending.
        /// </summary>
        PartiallyReady,

        /// <summary>
        /// Fully operational, all capabilities available.
        /// </summary>
        Ready,

        /// <summary>
        /// Operational but with reduced performance/features.
        /// </summary>
        Degraded
    }

    /// <summary>
    /// Describes a dependency on another plugin or interface.
    /// </summary>
    public class PluginDependency
    {
        /// <summary>
        /// Interface name required (e.g., "IMetadataIndex").
        /// </summary>
        public string RequiredInterface { get; init; } = string.Empty;

        /// <summary>
        /// Whether plugin can operate without this dependency.
        /// </summary>
        public bool IsOptional { get; init; }

        /// <summary>
        /// Human-readable explanation of why this dependency is needed.
        /// </summary>
        public string Reason { get; init; } = string.Empty;

        /// <summary>
        /// Minimum version required (optional).
        /// </summary>
        public Version? MinimumVersion { get; init; }
    }

    /// <summary>
    /// Describes a single capability provided by a plugin.
    /// Used for AI discovery and dynamic invocation.
    /// </summary>
    public class PluginCapabilityDescriptor
    {
        /// <summary>
        /// Unique capability identifier (e.g., "storage.local.save").
        /// Convention: "{category}.{plugin}.{action}"
        /// </summary>
        public string CapabilityId { get; init; } = string.Empty;

        /// <summary>
        /// Human-readable display name.
        /// </summary>
        public string DisplayName { get; init; } = string.Empty;

        /// <summary>
        /// AI-friendly description of what this capability does.
        /// </summary>
        public string Description { get; init; } = string.Empty;

        /// <summary>
        /// Category for grouping and filtering.
        /// </summary>
        public CapabilityCategory Category { get; init; }

        /// <summary>
        /// Whether this capability requires user approval.
        /// </summary>
        public bool RequiresApproval { get; init; }

        /// <summary>
        /// Minimum permission level required to invoke.
        /// </summary>
        public Permission RequiredPermission { get; init; }

        /// <summary>
        /// JSON Schema describing the parameters (as JSON string).
        /// Used by AI to construct valid invocation requests.
        /// </summary>
        public string ParameterSchemaJson { get; init; } = "{}";

        /// <summary>
        /// Tags for semantic search and discovery.
        /// </summary>
        public List<string> Tags { get; init; } = new();
    }

    /// <summary>
    /// Categories of capabilities for classification.
    /// </summary>
    public enum CapabilityCategory
    {
        Storage,
        Metadata,
        Security,
        Transform,
        Orchestration,
        Intelligence,
        Query,
        Maintenance,
        Diagnostic,
        Configuration
    }

    // ============================================================================
    // PLUGIN LIFECYCLE MESSAGES
    // ============================================================================

    /// <summary>
    /// Sent by plugin to notify kernel of state changes.
    /// Used when plugin transitions from Initializing → Ready.
    /// </summary>
    public class PluginStateChangedEvent
    {
        public string PluginId { get; init; } = string.Empty;
        public PluginReadyState OldState { get; init; }
        public PluginReadyState NewState { get; init; }
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
        public string? Message { get; init; }
    }

    /// <summary>
    /// Health check request sent by Kernel to long-running plugins.
    /// </summary>
    public class HealthCheckRequest
    {
        public string KernelId { get; init; } = string.Empty;
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Health check response from plugin.
    /// </summary>
    public class HealthCheckResponse
    {
        public string PluginId { get; init; } = string.Empty;
        public PluginReadyState CurrentState { get; init; }
        public bool IsHealthy { get; init; }
        public string? StatusMessage { get; init; }
        public Dictionary<string, object> Metrics { get; init; } = new();
    }

    /// <summary>
    /// Sent by Kernel to gracefully shut down a plugin.
    /// </summary>
    public class ShutdownRequest
    {
        public string KernelId { get; init; } = string.Empty;
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
        public TimeSpan GracePeriod { get; init; } = TimeSpan.FromSeconds(30);
        public string? Reason { get; init; }
    }

    /// <summary>
    /// Plugin acknowledges shutdown request.
    /// </summary>
    public class ShutdownResponse
    {
        public string PluginId { get; init; } = string.Empty;
        public bool Success { get; init; }
        public string? ErrorMessage { get; init; }
    }

    /// <summary>
    /// Generic plugin message for future extensibility.
    /// </summary>
    public class PluginMessage
    {
        public string MessageId { get; init; } = Guid.NewGuid().ToString();
        public string PluginId { get; init; } = string.Empty;
        public string MessageType { get; init; } = string.Empty;
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
        public Dictionary<string, object> Payload { get; init; } = new();
    }
}
