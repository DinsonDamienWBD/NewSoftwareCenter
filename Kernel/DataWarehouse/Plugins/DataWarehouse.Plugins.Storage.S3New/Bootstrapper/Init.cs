using DataWarehouse.SDK.Contracts;
using DataWarehouse.Plugins.Storage.S3New.Engine;

namespace DataWarehouse.Plugins.Storage.S3New.Bootstrapper
{
    /// <summary>
    /// Plugin entry point for AWS S3 Cloud Storage.
    /// Provides production-ready S3 storage with AWS Signature V4 authentication.
    ///
    /// Supports AWS S3 and S3-compatible storage (MinIO, DigitalOcean Spaces, Wasabi, etc.).
    /// The engine implementation is in S3StorageEngine.cs.
    /// </summary>
    [PluginInfo(
        name: "AWS S3 Cloud Storage",
        description: "AWS S3 and S3-compatible cloud storage with 99.999999999% durability and unlimited scalability",
        author: "DataWarehouse Team",
        version: "1.0.0",
        category: PluginCategory.Storage
    )]
    public class S3StoragePlugin
    {
        /// <summary>
        /// Creates an instance of the storage engine.
        /// </summary>
        public static S3StorageEngine CreateInstance()
        {
            return new S3StorageEngine();
        }
    }
}
