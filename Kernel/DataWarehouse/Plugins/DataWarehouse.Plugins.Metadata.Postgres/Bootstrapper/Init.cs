using System;
using DataWarehouse.SDK.Contracts;
using DataWarehouse.Plugins.Metadata.Postgres.Engine;

namespace DataWarehouse.Plugins.Metadata.Postgres.Bootstrapper
{
    /// <summary>
    /// Plugin entry point for PostgreSQL Metadata Indexing.
    /// Provides production-grade indexing with advanced SQL and JSON support.
    ///
    /// The engine implementation is in PostgresMetadataEngine.cs.
    /// </summary>
    [PluginInfo(
        name: "PostgreSQL Metadata Index",
        description: "Production-grade metadata indexing with PostgreSQL, JSONB support, and high availability",
        author: "DataWarehouse Team",
        version: "1.0.0",
        category: PluginCategory.Metadata
    )]
    public class PostgresMetadataPlugin
    {
        /// <summary>
        /// Creates an instance of the metadata engine.
        /// </summary>
        public static PostgresMetadataEngine CreateInstance()
        {
            return new PostgresMetadataEngine();
        }
    }
}
