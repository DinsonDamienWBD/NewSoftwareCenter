using System;
using Metadata.Postgres.Engine;
using DataWarehouse.SDK.Contracts;

namespace Metadata.Postgres.Bootstrapper
{
    /// <summary>
    /// Plugin entry point for PostgreSQL Metadata Indexing.
    /// Provides production-grade indexing with advanced SQL and JSON support.
    ///
    /// The engine implementation is in PostgresMetadataEngine.cs.
    /// </summary>
    public class PostgresMetadataPlugin
    {
        /// <summary>
        /// Plugin metadata and registration.
        /// </summary>
        public static PluginInfo PluginInfo => new()
        {
            Id = "metadata.postgres",
            Name = "PostgreSQL Metadata Index",
            Version = new Version(1, 0, 0),
            Author = "DataWarehouse SDK",
            Description = "Production-grade metadata indexing with PostgreSQL, JSONB support, and high availability",
            Category = PluginCategory.Metadata,
            Tags = new[] { "metadata", "indexing", "postgresql", "postgres", "production", "sql" }
        };

        /// <summary>
        /// Creates an instance of the metadata engine.
        /// </summary>
        public static PostgresMetadataEngine CreateInstance()
        {
            return new PostgresMetadataEngine();
        }
    }
}
