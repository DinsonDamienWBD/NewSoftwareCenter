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

                        // Find and invoke the handler
                        if (_capabilityHandlers.TryGetValue(capabilityId, out var handler))
                        {
                            var result = await handler(parameters);
                            return MessageResponse.SuccessResponse(result);
                        }

                        return MessageResponse.ErrorResponse($"Unknown capability: {capabilityId}");

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
