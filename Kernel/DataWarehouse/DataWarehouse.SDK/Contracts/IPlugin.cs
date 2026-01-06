using System.Threading.Tasks;

namespace DataWarehouse.SDK.Contracts
{
    /// <summary>
    /// The base contract for ALL plugins (Crypto, Compression, Features).
    /// Uses message-based handshake protocol for decoupled, async initialization.
    /// </summary>
    public interface IPlugin
    {
        /// <summary>
        /// Unique Plugin ID (e.g., "DataWarehouse.Storage.Local").
        /// DEPRECATED: Use HandshakeResponse.PluginId instead.
        /// </summary>
        [System.Obsolete("Use HandshakeResponse.PluginId instead. This property is kept for backward compatibility.")]
        string Id { get; }

        /// <summary>
        /// Human-readable Name.
        /// DEPRECATED: Use HandshakeResponse.Name instead.
        /// </summary>
        [System.Obsolete("Use HandshakeResponse.Name instead. This property is kept for backward compatibility.")]
        string Name { get; }

        /// <summary>
        /// Semantic Version.
        /// DEPRECATED: Use HandshakeResponse.Version instead.
        /// </summary>
        [System.Obsolete("Use HandshakeResponse.Version instead. This property is kept for backward compatibility.")]
        string Version { get; }

        /// <summary>
        /// Called by the Kernel immediately after loading the DLL.
        /// DEPRECATED: Use OnHandshakeAsync instead.
        /// This method is kept for backward compatibility during migration.
        /// </summary>
        /// <param name="context">The kernel context providing logging and environment info.</param>
        [System.Obsolete("Use OnHandshakeAsync instead. This method will be removed in a future version.")]
        void Initialize(IKernelContext context);

        /// <summary>
        /// Handshake protocol handler - called by Kernel during plugin initialization.
        /// Plugin should self-initialize and respond with capabilities and readiness state.
        /// This is the PRIMARY initialization method for message-based architecture.
        /// </summary>
        /// <param name="request">Handshake request from Kernel containing environment info.</param>
        /// <returns>Handshake response with plugin identity, capabilities, and state.</returns>
        Task<HandshakeResponse> OnHandshakeAsync(HandshakeRequest request);

        /// <summary>
        /// Generic message handler for runtime communication.
        /// Used for health checks, configuration updates, and custom messages.
        /// Optional: Plugins can return Task.CompletedTask if not needed.
        /// </summary>
        /// <param name="message">The message to handle.</param>
        /// <returns>Task representing async message handling.</returns>
        Task OnMessageAsync(PluginMessage message)
        {
            // Default implementation: no-op
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Exposed by the Kernel to allow plugins to log or access core services.
    /// </summary>
    public interface IKernelContext
    {
        /// <summary>
        /// Gets the detected operating environment (Laptop, Server, etc.).
        /// </summary>
        OperatingMode Mode { get; }

        /// <summary>
        /// The root directory of the Data Warehouse instance.
        /// </summary>
        string RootPath { get; }

        /// <summary>
        /// Log information.
        /// </summary>
        /// <param name="message">The message to log.</param>
        void LogInfo(string message);

        /// <summary>
        /// Log error.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="ex">The optional exception.</param>
        void LogError(string message, Exception? ex = null);

        /// <summary>
        /// Log warning.
        /// </summary>
        /// <param name="message">The warning message.</param>
        void LogWarning(string message);

        /// <summary>
        /// Log debug info.
        /// </summary>
        /// <param name="message">The debug message.</param>
        void LogDebug(string message);

        /// <summary>
        /// Retrieves a specific type of plugin.
        /// </summary>
        /// <typeparam name="T">The plugin interface type.</typeparam>
        /// <returns>The best matching plugin or null.</returns>
        T? GetPlugin<T>() where T : class, IPlugin;

        /// <summary>
        /// Retrieves all plugins of a specific type.
        /// </summary>
        /// <typeparam name="T">The plugin interface type.</typeparam>
        /// <returns>A collection of matching plugins.</returns>
        System.Collections.Generic.IEnumerable<T> GetPlugins<T>() where T : class, IPlugin;
    }
}