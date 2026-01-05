namespace DataWarehouse.SDK.Contracts
{
    /// <summary>
    /// The base contract for ALL plugins (Crypto, Compression, Features).
    /// </summary>
    public interface IPlugin
    {
        /// <summary>
        /// Unique Plugin ID (e.g., "DataWarehouse.Storage.Local").
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Human-readable Name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Semantic Version.
        /// </summary>
        string Version { get; }

        /// <summary>
        /// Called by the Kernel immediately after loading the DLL.
        /// Use this to register services or read initial config.
        /// </summary>
        /// <param name="context">The kernel context providing logging and environment info.</param>
        void Initialize(IKernelContext context);
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