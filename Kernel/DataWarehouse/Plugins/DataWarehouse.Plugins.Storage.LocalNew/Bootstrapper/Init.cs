using DataWarehouse.SDK.Contracts;
using DataWarehouse.Plugins.Storage.LocalNew.Engine;

namespace DataWarehouse.Plugins.Storage.LocalNew.Bootstrapper
{
    /// <summary>
    /// Plugin entry point for Local Filesystem Storage.
    /// Provides production-ready local storage with atomic writes and cross-platform compatibility.
    ///
    /// This plugin is registered automatically when the SDK scans the Plugins directory.
    /// The engine implementation is in LocalStorageEngine.cs.
    /// </summary>
    public class LocalStoragePlugin
    {
        /// <summary>
        /// Plugin metadata and registration.
        /// </summary>
        public static PluginInfo PluginInfo => new()
        {
            Id = "storage.local",
            Name = "Local Filesystem Storage",
            Version = new Version(1, 0, 0),
            Author = "DataWarehouse SDK",
            Description = "Cross-platform local filesystem storage with atomic writes and subdirectory support",
            Category = PluginCategory.Storage,
            Tags = new[] { "storage", "filesystem", "local", "development", "caching" }
        };

        /// <summary>
        /// Creates an instance of the storage engine.
        /// </summary>
        public static LocalStorageEngine CreateInstance()
        {
            return new LocalStorageEngine();
        }
    }
}
