using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DataWarehouse.SDK.Contracts
{
    /// <summary>
    /// Base class for all DataWarehouse plugins.
    /// Implements the handshake protocol and message routing once for all plugins.
    /// Plugins inherit from category-specific bases (PipelinePluginBase, StorageProviderBase, etc.)
    /// which in turn inherit from this class.
    ///
    /// Key responsibilities:
    /// - Handles handshake protocol (OnHandshakeAsync)
    /// - Routes messages to capability handlers (OnMessageAsync)
    /// - Manages capability registration
    /// - Provides common lifecycle methods
    ///
    /// Plugins only need to:
    /// 1. Set metadata in constructor
    /// 2. Declare capabilities
    /// 3. Implement their specific business logic
    /// </summary>
    public abstract class PluginBase : IPlugin
    {
        // =========================================================================
        // PRIVATE FIELDS - Managed internally by base class
        // =========================================================================

        /// <summary>
        /// Dictionary mapping capability IDs to their handler functions.
        /// Populated by derived classes calling RegisterCapability().
        /// </summary>
        private readonly Dictionary<string, Func<Dictionary<string, object>, Task<object?>>> _capabilityHandlers = new();

        // =========================================================================
        // PROTECTED PROPERTIES - Accessible to derived classes
        // =========================================================================

        /// <summary>
        /// The kernel context provided during initialization.
        /// Gives plugins access to logging, root path, operating mode, etc.
        /// Null until handshake completes.
        /// </summary>
        protected IKernelContext? Context { get; private set; }

        /// <summary>
        /// Unique identifier for this plugin (e.g., "DataWarehouse.Storage.Local").
        /// Set once in constructor by derived class.
        /// </summary>
        protected string PluginId { get; }

        /// <summary>
        /// Human-readable name of this plugin (e.g., "Local File System Storage").
        /// Set once in constructor by derived class.
        /// </summary>
        protected string PluginName { get; }

        /// <summary>
        /// Semantic version of this plugin.
        /// Set once in constructor by derived class.
        /// </summary>
        protected Version PluginVersion { get; }

        /// <summary>
        /// Category this plugin belongs to (Pipeline, Storage, Metadata, etc.).
        /// Set once in constructor by derived class or category-specific base.
        /// </summary>
        protected PluginCategory PluginCategory { get; }

        // =========================================================================
        // ABSTRACT MEMBERS - Must be implemented by derived classes
        // =========================================================================

        /// <summary>
        /// Plugin declares its capabilities here.
        /// Each capability represents a function the plugin can perform.
        /// Used by AI for dynamic discovery and by Kernel for routing messages.
        /// </summary>
        protected abstract PluginCapabilityDescriptor[] Capabilities { get; }

        /// <summary>
        /// Plugin initializes its internal state here.
        /// Called during handshake after Context is set.
        /// Use this to:
        /// - Register capability handlers via RegisterCapability()
        /// - Set up internal data structures
        /// - Load configuration
        /// </summary>
        /// <param name="context">Kernel context for logging and environment info.</param>
        protected abstract void InitializeInternal(IKernelContext context);

        // =========================================================================
        // VIRTUAL MEMBERS - Can be overridden by derived classes if needed
        // =========================================================================

        /// <summary>
        /// Plugin declares its dependencies here (optional).
        /// Return empty array if no dependencies.
        /// </summary>
        protected virtual PluginDependency[] Dependencies => Array.Empty<PluginDependency>();

        // =========================================================================
        // AI-NATIVE VIRTUAL MEMBERS - For AI understanding and optimization
        // =========================================================================

        /// <summary>
        /// Natural language description of what this plugin does.
        /// Used by AI for semantic understanding and natural language queries.
        ///
        /// Example: "Compresses data using GZip algorithm. Fast compression with good ratio for text and binary data."
        ///
        /// Should be:
        /// - Clear and concise (1-2 sentences)
        /// - Describe WHAT the plugin does, not HOW
        /// - Include key characteristics (speed, quality, use cases)
        /// - Written for human understanding
        /// </summary>
        protected virtual string SemanticDescription => $"{PluginName} - No semantic description provided";

        /// <summary>
        /// Semantic tags for AI categorization and search.
        /// Used by AI for capability discovery via natural language.
        ///
        /// Examples:
        /// - Compression: ["compression", "fast", "lossless", "standard"]
        /// - Encryption: ["encryption", "aes", "secure", "symmetric"]
        /// - Storage: ["storage", "local", "filesystem", "persistent"]
        ///
        /// Should include:
        /// - Category tags (what domain?)
        /// - Characteristic tags (fast/slow, lossy/lossless, etc.)
        /// - Technology tags (algorithm names, protocols)
        /// - Use case tags (when to use this?)
        /// </summary>
        protected virtual string[] SemanticTags => Array.Empty<string>();

        /// <summary>
        /// Performance characteristics of this plugin.
        /// Used by AI for optimization decisions and cost estimation.
        ///
        /// Override to provide accurate performance metrics based on:
        /// - Benchmarking results
        /// - Historical data
        /// - Theoretical analysis
        ///
        /// AI uses this to:
        /// - Choose between alternative plugins
        /// - Estimate execution time
        /// - Predict resource usage
        /// - Optimize execution plans
        /// </summary>
        protected virtual PerformanceCharacteristics PerformanceProfile => new()
        {
            AverageLatencyMs = 0,
            ThroughputBytesPerSecond = 0,
            MemoryUsageBytes = 0,
            CpuUsagePercent = 0,
            CostPerOperationUsd = 0
        };

        /// <summary>
        /// Relationships between this plugin's capabilities and other capabilities.
        /// Used by AI for execution planning and capability chaining.
        ///
        /// Examples:
        /// - Compression flows into encryption
        /// - Encryption flows into storage
        /// - SQL interface depends on metadata index
        ///
        /// Relationship types:
        /// - "flows_into": Output of this capability can feed into target capability
        /// - "depends_on": This capability requires target capability to function
        /// - "alternative_to": This capability can substitute for target capability
        /// - "compatible_with": This capability can work alongside target capability
        /// - "incompatible_with": This capability conflicts with target capability
        /// </summary>
        protected virtual CapabilityRelationship[] CapabilityRelationships => Array.Empty<CapabilityRelationship>();

        /// <summary>
        /// Example usage scenarios for AI to learn from.
        /// Used by AI to understand common use cases and patterns.
        ///
        /// Each example should show:
        /// - User intent (natural language)
        /// - Capability invocation
        /// - Parameters used
        /// - Expected outcome
        ///
        /// AI uses examples to:
        /// - Match user intent to capabilities
        /// - Learn typical parameter values
        /// - Understand success patterns
        /// </summary>
        protected virtual PluginUsageExample[] UsageExamples => Array.Empty<PluginUsageExample>();

        // =========================================================================
        // EVENT EMISSION HOOKS - For AI observability and proactive agents
        // =========================================================================

        /// <summary>
        /// Called before a capability is invoked.
        /// Override to emit custom events, perform validation, or log execution.
        ///
        /// Use cases:
        /// - Emit BlobAccessedEvent for storage operations
        /// - Log capability usage for audit
        /// - Validate preconditions
        /// - Collect performance metrics
        /// </summary>
        /// <param name="capabilityId">The capability about to be invoked.</param>
        /// <param name="parameters">Parameters passed to capability.</param>
        protected virtual void OnBeforeCapabilityInvoked(string capabilityId, Dictionary<string, object> parameters)
        {
            // Default: no-op
            // Plugins can override to emit events or perform pre-processing
        }

        /// <summary>
        /// Called after a capability completes successfully.
        /// Override to emit custom events, collect metrics, or trigger follow-up actions.
        ///
        /// Use cases:
        /// - Emit BlobStoredEvent after successful write
        /// - Collect performance metrics (duration, throughput)
        /// - Trigger proactive optimization agents
        /// - Update health status
        /// </summary>
        /// <param name="capabilityId">The capability that was invoked.</param>
        /// <param name="parameters">Parameters passed to capability.</param>
        /// <param name="result">Result returned by capability.</param>
        /// <param name="durationMs">Execution duration in milliseconds.</param>
        protected virtual void OnAfterCapabilityInvoked(
            string capabilityId,
            Dictionary<string, object> parameters,
            object? result,
            double durationMs)
        {
            // Default: no-op
            // Plugins can override to emit events or collect metrics
        }

        /// <summary>
        /// Called when a capability invocation fails with an exception.
        /// Override to emit error events, log failures, or trigger recovery.
        ///
        /// Use cases:
        /// - Emit CapabilityFailedEvent for monitoring
        /// - Log errors with context
        /// - Trigger retry or recovery mechanisms
        /// - Update health status
        /// </summary>
        /// <param name="capabilityId">The capability that failed.</param>
        /// <param name="parameters">Parameters passed to capability.</param>
        /// <param name="exception">Exception that was thrown.</param>
        protected virtual void OnCapabilityFailed(
            string capabilityId,
            Dictionary<string, object> parameters,
            Exception exception)
        {
            // Default: log error
            Context?.LogError($"Capability '{capabilityId}' failed: {exception.Message}", exception);
        }

        /// <summary>
        /// Emits a custom event for AI observability and proactive agents.
        /// Events can be observed by:
        /// - Proactive optimization agents
        /// - AI Runtime for learning and adaptation
        /// - Monitoring and telemetry systems
        /// - External integrations
        ///
        /// Common event types:
        /// - "BlobStored": After writing data
        /// - "BlobAccessed": After reading data
        /// - "BlobDeleted": After deleting data
        /// - "PluginStateChanged": When plugin state changes
        /// - "PerformanceMetric": Performance data points
        ///
        /// Note: Actual event bus wiring implemented by Kernel.
        /// This method provides a hook for plugins to emit events.
        /// </summary>
        /// <param name="eventType">Type of event (e.g., "BlobStored").</param>
        /// <param name="eventData">Event payload (will be serialized).</param>
        protected void EmitEvent(string eventType, object eventData)
        {
            // TODO: Wire up to event bus when implemented
            // For now, just log at debug level
            Context?.LogDebug($"Event emitted: {eventType}");
        }

        /// <summary>
        /// Health check handler (optional override).
        /// Default implementation returns "healthy".
        /// Override to provide custom health status.
        /// </summary>
        /// <returns>Health status as MessageResponse.</returns>
        protected virtual MessageResponse OnHealthCheck()
        {
            return MessageResponse.SuccessResponse(new { status = "healthy", plugin = PluginId });
        }

        /// <summary>
        /// Shutdown handler (optional override).
        /// Called when Kernel is shutting down.
        /// Use this to clean up resources, close connections, etc.
        /// </summary>
        /// <returns>Task representing async shutdown operation.</returns>
        protected virtual Task OnShutdownAsync()
        {
            return Task.CompletedTask;
        }

        // =========================================================================
        // CONSTRUCTOR
        // =========================================================================

        /// <summary>
        /// Constructs a new plugin with the specified metadata.
        /// Called by derived class constructors.
        /// </summary>
        /// <param name="id">Unique plugin identifier.</param>
        /// <param name="name">Human-readable plugin name.</param>
        /// <param name="version">Plugin version.</param>
        /// <param name="category">Plugin category.</param>
        protected PluginBase(
            string id,
            string name,
            Version version,
            PluginCategory category)
        {
            PluginId = id ?? throw new ArgumentNullException(nameof(id));
            PluginName = name ?? throw new ArgumentNullException(nameof(name));
            PluginVersion = version ?? throw new ArgumentNullException(nameof(version));
            PluginCategory = category;
        }

        // =========================================================================
        // IPLUGIN IMPLEMENTATION - Backward compatibility properties (deprecated)
        // =========================================================================

        /// <summary>
        /// Plugin ID (deprecated - use HandshakeResponse instead).
        /// Kept for backward compatibility with legacy loader.
        /// </summary>
        [Obsolete("Use HandshakeResponse.PluginId instead")]
        public string Id => PluginId;

        /// <summary>
        /// Plugin name (deprecated - use HandshakeResponse instead).
        /// Kept for backward compatibility with legacy loader.
        /// </summary>
        [Obsolete("Use HandshakeResponse.Name instead")]
        public string Name => PluginName;

        /// <summary>
        /// Plugin version (deprecated - use HandshakeResponse instead).
        /// Kept for backward compatibility with legacy loader.
        /// </summary>
        [Obsolete("Use HandshakeResponse.Version instead")]
        public string Version => PluginVersion.ToString();

        /// <summary>
        /// Legacy initialization method (deprecated).
        /// Use OnHandshakeAsync instead.
        /// Kept for backward compatibility with old plugins.
        /// </summary>
        /// <param name="context">Kernel context.</param>
        [Obsolete("Use OnHandshakeAsync instead")]
        public virtual void Initialize(IKernelContext context)
        {
            Context = context;
            InitializeInternal(context);
        }

        // =========================================================================
        // HANDSHAKE PROTOCOL IMPLEMENTATION (implemented once for all plugins)
        // =========================================================================

        /// <summary>
        /// Handles the handshake protocol.
        /// This method is IMPLEMENTED ONCE for all plugins - no need to override.
        ///
        /// Flow:
        /// 1. Creates kernel context from request
        /// 2. Calls InitializeInternal() (plugin-specific initialization)
        /// 3. Collects metadata, capabilities, dependencies
        /// 4. Returns HandshakeResponse with all plugin information
        ///
        /// Plugins don't override this - they implement InitializeInternal() instead.
        /// </summary>
        /// <param name="request">Handshake request from Kernel.</param>
        /// <returns>Handshake response with plugin metadata and capabilities.</returns>
        public virtual async Task<HandshakeResponse> OnHandshakeAsync(HandshakeRequest request)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                // Create kernel context from request
                Context = new KernelContextWrapper(request);

                // Call plugin-specific initialization
                InitializeInternal(Context);

                // Build and return successful handshake response
                return HandshakeResponse.Success(
                    pluginId: PluginId,
                    name: PluginName,
                    version: PluginVersion,
                    category: PluginCategory,
                    capabilities: Capabilities.ToList(),
                    initDuration: DateTime.UtcNow - startTime
                );
            }
            catch (Exception ex)
            {
                // Return failure response with exception details
                Context?.LogError($"Plugin '{PluginName}' handshake failed", ex);
                return HandshakeResponse.Failure(
                    pluginId: PluginId,
                    name: PluginName,
                    errorMessage: $"Initialization failed: {ex.Message}"
                );
            }
        }

        // =========================================================================
        // MESSAGE HANDLING IMPLEMENTATION (implemented once for all plugins)
        // =========================================================================

        /// <summary>
        /// Handles runtime messages from Kernel.
        /// This method is IMPLEMENTED ONCE for all plugins - no need to override.
        ///
        /// Routes messages to appropriate handlers:
        /// - InvokeCapability: Routes to registered capability handler
        /// - HealthCheck: Calls OnHealthCheck()
        /// - Shutdown: Calls OnShutdownAsync()
        ///
        /// Plugins don't override this - they register handlers via RegisterCapability().
        /// </summary>
        /// <param name="message">The message to handle.</param>
        /// <returns>MessageResponse with result or error.</returns>
        public virtual async Task<MessageResponse> OnMessageAsync(PluginMessage message)
        {
            try
            {
                switch (message.MessageType)
                {
                    case "InvokeCapability":
                        // Extract capability ID and parameters from message
                        var capabilityId = (string)message.Payload["capabilityId"];
                        var parameters = (Dictionary<string, object>)message.Payload["parameters"];

                        // Find handler
                        if (!_capabilityHandlers.TryGetValue(capabilityId, out var handler))
                        {
                            return MessageResponse.ErrorResponse($"Unknown capability: {capabilityId}");
                        }

                        // Call pre-invocation hook
                        OnBeforeCapabilityInvoked(capabilityId, parameters);

                        // Invoke capability with timing
                        var startTime = DateTime.UtcNow;
                        try
                        {
                            var result = await handler(parameters);
                            var durationMs = (DateTime.UtcNow - startTime).TotalMilliseconds;

                            // Call post-invocation hook
                            OnAfterCapabilityInvoked(capabilityId, parameters, result, durationMs);

                            return MessageResponse.SuccessResponse(result);
                        }
                        catch (Exception handlerEx)
                        {
                            // Call failure hook
                            OnCapabilityFailed(capabilityId, parameters, handlerEx);
                            throw; // Re-throw to be caught by outer catch block
                        }

                    case "HealthCheck":
                        // Health check request
                        return OnHealthCheck();

                    case "Shutdown":
                        // Shutdown request
                        await OnShutdownAsync();
                        return MessageResponse.SuccessResponse(new { shutdown = true });

                    default:
                        return MessageResponse.ErrorResponse($"Unknown message type: {message.MessageType}");
                }
            }
            catch (Exception ex)
            {
                Context?.LogError($"Plugin '{PluginName}' message handling failed", ex);
                return MessageResponse.FromException(ex);
            }
        }

        // =========================================================================
        // HELPER METHODS FOR DERIVED CLASSES
        // =========================================================================

        /// <summary>
        /// Registers a capability handler.
        /// Called by derived classes during InitializeInternal().
        ///
        /// Example:
        ///   RegisterCapability("storage.local.save", async (parameters) => {
        ///       var key = (string)parameters["key"];
        ///       var data = (byte[])parameters["data"];
        ///       await SaveAsync(key, data);
        ///       return new { success = true };
        ///   });
        /// </summary>
        /// <param name="capabilityId">Unique capability identifier.</param>
        /// <param name="handler">Async function that handles this capability.</param>
        protected void RegisterCapability(
            string capabilityId,
            Func<Dictionary<string, object>, Task<object?>> handler)
        {
            if (string.IsNullOrWhiteSpace(capabilityId))
                throw new ArgumentException("Capability ID cannot be empty", nameof(capabilityId));

            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            _capabilityHandlers[capabilityId] = handler;
            Context?.LogDebug($"Registered capability: {capabilityId}");
        }

        // =========================================================================
        // KERNEL CONTEXT WRAPPER (lightweight implementation for handshake)
        // =========================================================================

        /// <summary>
        /// Lightweight kernel context implementation for plugin initialization.
        /// Wraps HandshakeRequest to provide IKernelContext interface.
        ///
        /// Note: Real logging and plugin access should be implemented by Kernel.
        /// This is just for initial handshake and plugin bootstrapping.
        /// </summary>
        private class KernelContextWrapper : IKernelContext
        {
            private readonly HandshakeRequest _request;

            /// <summary>
            /// Constructs a kernel context wrapper from handshake request.
            /// </summary>
            /// <param name="request">The handshake request.</param>
            public KernelContextWrapper(HandshakeRequest request)
            {
                _request = request ?? throw new ArgumentNullException(nameof(request));
            }

            /// <summary>
            /// Operating mode from handshake request.
            /// </summary>
            public OperatingMode Mode => _request.Mode;

            /// <summary>
            /// Root path from handshake request.
            /// </summary>
            public string RootPath => _request.RootPath;

            // Logging methods (no-op in wrapper - real logging done by Kernel)
            /// <summary>Logs info message (no-op in wrapper).</summary>
            public void LogInfo(string message) { /* TODO: Route to Kernel logger */ }

            /// <summary>Logs error message (no-op in wrapper).</summary>
            public void LogError(string message, Exception? ex = null) { /* TODO: Route to Kernel logger */ }

            /// <summary>Logs warning message (no-op in wrapper).</summary>
            public void LogWarning(string message) { /* TODO: Route to Kernel logger */ }

            /// <summary>Logs debug message (no-op in wrapper).</summary>
            public void LogDebug(string message) { /* TODO: Route to Kernel logger */ }

            // Deprecated plugin access methods
            /// <summary>Deprecated - use message-based communication instead.</summary>
            [Obsolete("Use message-based communication")]
            public T? GetPlugin<T>() where T : class, IPlugin => null;

            /// <summary>Deprecated - use message-based communication instead.</summary>
            [Obsolete("Use message-based communication")]
            public IEnumerable<T> GetPlugins<T>() where T : class, IPlugin => Enumerable.Empty<T>();
        }
    }
}
