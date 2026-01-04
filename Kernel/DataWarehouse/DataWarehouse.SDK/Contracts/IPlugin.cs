namespace DataWarehouse.SDK.Contracts
{
    /// <summary>
    /// The base contract for ALL plugins (Crypto, Compression, Features).
    /// </summary>
    public interface IPlugin
    {
        /// <summary>
        /// Plugin ID
        /// e.g. DataWarehouse.Crypto
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Name
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Plugin version
        /// </summary>
        string Version { get; }

        /// <summary>
        /// Called by the Kernel immediately after loading the DLL.
        /// Use this to register services or read initial config.
        /// </summary>
        void Initialize(IKernelContext context);
    }

    /// <summary>
    /// Exposed by the Kernel to allow plugins to log or access core services.
    /// </summary>
    public interface IKernelContext
    {
        /// <summary>
        /// Log information
        /// </summary>
        /// <param name="message"></param>
        void LogInfo(string message);

        /// <summary>
        /// Log message
        /// </summary>
        /// <param name="message"></param>
        /// <param name="ex"></param>
        void LogError(string message, Exception? ex = null);

        /// <summary>
        /// Log warning
        /// </summary>
        /// <param name="message"></param>
        void LogWarning(string message);

        /// <summary>
        /// Log Debug
        /// </summary>
        /// <param name="message"></param>
        void LogDebug(string message);

        /// <summary>
        /// Root path
        /// </summary>
        string RootPath { get; }

        /// <summary>
        /// Get plugin
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        T? GetPlugin<T>() where T : class, IPlugin;

        /// <summary>
        /// Get plugins
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        IEnumerable<T> GetPlugins<T>() where T : class, IPlugin;
    }
}