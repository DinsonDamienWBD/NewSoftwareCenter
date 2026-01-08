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
    public class IPFSStoragePlugin
    {
        /// <summary>
        /// Plugin metadata and registration.
        /// </summary>
        public static PluginInfo PluginInfo => new()
        {
            Id = "storage.ipfs",
            Name = "IPFS Distributed Storage",
            Version = new Version(1, 0, 0),
            Author = "DataWarehouse SDK",
            Description = "Distributed content-addressed storage on the IPFS network with automatic deduplication",
            Category = PluginCategory.Storage,
            Tags = new[] { "storage", "ipfs", "distributed", "p2p", "web3", "decentralized" }
        };

        /// <summary>
        /// Creates an instance of the storage engine.
        /// </summary>
        public static IPFSStorageEngine CreateInstance()
        {
            return new IPFSStorageEngine();
        }
    }
}
