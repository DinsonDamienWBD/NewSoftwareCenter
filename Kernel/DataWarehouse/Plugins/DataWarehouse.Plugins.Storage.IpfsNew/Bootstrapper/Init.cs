using DataWarehouse.SDK.Contracts;
using DataWarehouse.Plugins.Storage.IpfsNew.Engine;

namespace DataWarehouse.Plugins.Storage.IpfsNew.Bootstrapper
{
    /// <summary>
    /// Plugin entry point for IPFS Distributed Storage.
    /// Provides production-ready IPFS storage with content-addressing and automatic deduplication.
    ///
    /// The engine implementation is in IPFSStorageEngine.cs.
    /// </summary>
    [PluginInfo(
        name: "IPFS Distributed Storage",
        description: "Distributed content-addressed storage on the IPFS network with automatic deduplication",
        author: "DataWarehouse Team",
        version: "1.0.0",
        category: PluginCategory.Storage
    )]
    public class IPFSStoragePlugin
    {
        /// <summary>
        /// Creates an instance of the storage engine.
        /// </summary>
        public static IPFSStorageEngine CreateInstance()
        {
            return new IPFSStorageEngine();
        }
    }
}
