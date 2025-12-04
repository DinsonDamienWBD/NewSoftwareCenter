using System.Threading.Tasks;
using SoftwareCenter.Core.Kernel;

namespace SoftwareCenter.Core.Modules
{
    /// <summary>
    /// The contract every feature module (including the Kernel) must implement.
    /// This allows the Host to load/unload DLLs dynamically without knowing what's inside.
    /// </summary>
    public interface IModule
    {
        /// <summary>
        /// Called immediately after the module is loaded.
        /// Use this to register routes, open database connections, or load configurations.
        /// </summary>
        /// <param name="kernel">The router instance to register commands or listen for events.</param>
        Task InitializeAsync(IKernel kernel);

        /// <summary>
        /// Called before the module is disposed.
        /// CRITICAL: You must release file handles, stop timers, and close DB connections here.
        /// If this fails or hangs, the application might require a force kill.
        /// </summary>
        Task UnloadAsync();

        /// <summary>
        /// Helper to identify the module version/name without reflection.
        /// </summary>
        string ModuleName { get; }
        string Version { get; }
        // Lifecycle
        bool IsInitialized { get; }
    }
}