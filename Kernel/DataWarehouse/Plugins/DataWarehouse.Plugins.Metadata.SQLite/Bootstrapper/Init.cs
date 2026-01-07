using System;
using Metadata.SQLite.Engine;
using DataWarehouse.SDK.Contracts;

namespace Metadata.SQLite.Bootstrapper
{
    /// <summary>
    /// Plugin entry point for SQLite Metadata Indexing.
    /// Provides local file-based indexing with full SQL query support.
    ///
    /// The engine implementation is in SQLiteMetadataEngine.cs.
    /// </summary>
    public class SQLiteMetadataPlugin
    {
        /// <summary>
        /// Plugin metadata and registration.
        /// </summary>
        public static PluginInfo PluginInfo => new()
        {
            Id = "metadata.sqlite",
            Name = "SQLite Metadata Index",
            Version = new Version(1, 0, 0),
            Author = "DataWarehouse SDK",
            Description = "Local file-based metadata indexing with SQL support and ACID transactions",
            Category = PluginCategory.Metadata,
            Tags = new[] { "metadata", "indexing", "sqlite", "sql", "local", "development" }
        };

        /// <summary>
        /// Creates an instance of the metadata engine.
        /// </summary>
        public static SQLiteMetadataEngine CreateInstance()
        {
            return new SQLiteMetadataEngine();
        }
    }
}
