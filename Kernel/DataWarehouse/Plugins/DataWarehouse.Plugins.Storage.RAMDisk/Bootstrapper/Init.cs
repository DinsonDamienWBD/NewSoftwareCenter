using DataWarehouse.Plugins.Storage.RAMDisk.Engine;
using DataWarehouse.SDK.Contracts;

namespace DataWarehouse.Plugins.Storage.RAMDisk.Bootstrapper
{
    /// <summary>
    /// AI-Native RAMDisk Storage Plugin.
    /// Ultra-high-performance in-memory storage with optional persistence.
    /// </summary>
    [PluginInfo(
        name: "RAMDisk Storage",
        description: "Ultra-high-performance in-memory storage with optional persistence and automatic LRU eviction",
        author: "DataWarehouse Team",
        version: "1.0.0",
        category: PluginCategory.Storage
    )]
    public class RAMDiskStoragePlugin
    {
        /// <summary>
        /// Plugin metadata and registration.
        /// </summary>
        public static PluginInfo PluginInfo => new()
        {
            Id = "storage.RAMDisk",
            Name = "RAMDisk Storage",
            Version = new Version(1, 0, 0),
            Author = "DataWarehouse SDK",
            Description = "RAMDisk for blazing fast storage",
            Category = PluginCategory.Storage,
            Tags = new[] { "storage", "filesystem", "local", "RAM", "memory", "development", "caching" }
        };

        /// <summary>
        /// Creates an instance of the storage engine.
        /// </summary>
        public static RAMDiskStoragePlugin CreateInstance()
        {
            return new RAMDiskStoragePlugin();
        }
    }
}
