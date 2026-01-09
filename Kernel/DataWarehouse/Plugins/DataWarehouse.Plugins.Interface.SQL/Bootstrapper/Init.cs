using DataWarehouse.SDK.Contracts;
using DataWarehouse.Plugins.Interface.SQL.Engine;

namespace DataWarehouse.Plugins.Interface.SQL.Bootstrapper
{
    [PluginInfo(
        name: "SQL Query Interface",
        description: "Query data using SQL with PostgreSQL wire protocol compatibility",
        author: "DataWarehouse Team",
        version: "1.0.0",
        category: PluginCategory.Interface
    )]
    public class SQLInterfacePlugin
    {
        public static PluginInfo PluginInfo => new()
        {
            Id = "interface.sql",
            Name = "SQL Query Interface",
            Version = new Version(1, 0, 0),
            Author = "DataWarehouse SDK",
            Description = "Query data using SQL with PostgreSQL wire protocol compatibility",
            Category = PluginCategory.Interface,
            Tags = new[] { "interface", "sql", "query", "postgresql", "database" }
        };

        public static SQLInterfaceEngine CreateInstance() => new();
    }
}
