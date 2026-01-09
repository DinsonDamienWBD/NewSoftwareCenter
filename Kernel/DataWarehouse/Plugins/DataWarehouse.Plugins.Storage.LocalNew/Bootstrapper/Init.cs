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
    [PluginInfo(
        name: "Local Filesystem Storage",
        description: "Cross-platform local filesystem storage with atomic writes and subdirectory support",
        author: "DataWarehouse Team",
        version: "1.0.0",
        category: PluginCategory.Storage
    )]
    public class LocalStoragePlugin
    {
        /// <summary>
        /// Creates an instance of the storage engine.
        /// </summary>
        public static LocalStorageEngine CreateInstance()
        {
            return new LocalStorageEngine();
        }
    }
}
