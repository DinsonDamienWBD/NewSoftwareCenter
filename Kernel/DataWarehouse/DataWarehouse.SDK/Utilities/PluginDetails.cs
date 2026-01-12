using DataWarehouse.SDK.Primitives;
using DataWarehouse.SDK.Security;

namespace DataWarehouse.SDK.Utilities
{
    /// <summary>
    /// Describes a plugin's identity and metadata.
    /// </summary>
    public class PluginDescriptor
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Version { get; init; } = string.Empty;
        public PluginCategory Category { get; init; }
        public string Description { get; init; } = string.Empty;
        public List<string> Tags { get; init; } = new();
        public Dictionary<string, object> Metadata { get; init; } = new();
    }

    public class PluginDependency
    {
        public string RequiredInterface { get; init; } = string.Empty;  // "IMetadataIndex"
        public bool IsOptional { get; init; }
        public string Reason { get; init; } = string.Empty;  // "Needed for manifest lookups"
    }

    public class PluginCapabilityDescriptor
    {
        public string CapabilityId { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public CapabilityCategory Category { get; init; }
        public bool RequiresApproval { get; init; }
        public Permission RequiredPermission { get; init; }
        // JSON schema as string (or JObject)
        public string ParameterSchemaJson { get; init; } = "{}";
    }

    /// <summary>
    /// Provides execution context for plugin capability invocations.
    /// AI-native context with access to kernel services and security.
    /// </summary>
    public interface IExecutionContext
    {
        /// <summary>
        /// The user or agent invoking this capability.
        /// </summary>
        string UserId { get; }

        /// <summary>
        /// Security context for permission checks.
        /// </summary>
        ISecurityContext SecurityContext { get; }

        /// <summary>
        /// Access to kernel logging services.
        /// </summary>
        void LogInfo(string message);
        void LogWarning(string message);
        void LogError(string message, Exception? ex = null);

        /// <summary>
        /// AI-native: Allows plugins to request confirmation from AI agents or users.
        /// </summary>
        Task<bool> RequestApprovalAsync(string message, string? reason = null);

        /// <summary>
        /// AI-native: Get a capability result from another plugin (for chaining operations).
        /// </summary>
        Task<object?> InvokeCapabilityAsync(string capabilityId, Dictionary<string, object> parameters);

        /// <summary>
        /// Get configuration value for the plugin.
        /// </summary>
        T? GetConfig<T>(string key, T? defaultValue = default);

        /// <summary>
        /// Cancellation token for long-running operations.
        /// </summary>
        CancellationToken CancellationToken { get; }
    }

    /// <summary>
    /// Represents a message sent to a plugin for external signals or events.
    /// AI-native: supports both structured and unstructured messages.
    /// </summary>
    public class PluginMessage
    {
        /// <summary>
        /// Message type identifier (e.g., "config.changed", "system.shutdown", "ai.query").
        /// </summary>
        public string Type { get; init; } = string.Empty;

        /// <summary>
        /// Message payload (can be any object, JSON-serializable).
        /// </summary>
        public object? Payload { get; init; }

        /// <summary>
        /// Timestamp of the message.
        /// </summary>
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Source of the message (e.g., "Kernel", "AI Agent", "User").
        /// </summary>
        public string Source { get; init; } = string.Empty;

        /// <summary>
        /// Optional correlation ID for tracking related messages.
        /// </summary>
        public string? CorrelationId { get; init; }

        /// <summary>
        /// AI-native: Natural language description of the message for LLM processing.
        /// </summary>
        public string? Description { get; init; }

        /// <summary>
        /// Additional metadata for the message.
        /// </summary>
        public Dictionary<string, object> Metadata { get; init; } = new();
    }
}
