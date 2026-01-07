using System;
using Storage.S3.Engine;
using DataWarehouse.SDK.Contracts;

namespace Storage.S3.Bootstrapper
{
    /// <summary>
    /// Plugin entry point for AWS S3 Cloud Storage.
    /// Provides production-ready S3 storage with AWS Signature V4 authentication.
    ///
    /// Supports AWS S3 and S3-compatible storage (MinIO, DigitalOcean Spaces, Wasabi, etc.).
    /// The engine implementation is in S3StorageEngine.cs.
    /// </summary>
    public class S3StoragePlugin
    {
        /// <summary>
        /// Plugin metadata and registration.
        /// </summary>
        public static PluginInfo PluginInfo => new()
        {
            Id = "storage.s3",
            Name = "AWS S3 Cloud Storage",
            Version = new Version(1, 0, 0),
            Author = "DataWarehouse SDK",
            Description = "AWS S3 and S3-compatible cloud storage with 99.999999999% durability and unlimited scalability",
            Category = PluginCategory.Storage,
            Tags = new[] { "storage", "cloud", "s3", "aws", "production", "scalable" }
        };

        /// <summary>
        /// Creates an instance of the storage engine.
        /// </summary>
        public static S3StorageEngine CreateInstance()
        {
            return new S3StorageEngine();
        }
    }
}
