using DataWarehouse.SDK.Contracts;
using DataWarehouse.Plugins.Interface.SQL.Engine;

namespace DataWarehouse.Plugins.Interface.SQL.Bootstrapper
{
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
