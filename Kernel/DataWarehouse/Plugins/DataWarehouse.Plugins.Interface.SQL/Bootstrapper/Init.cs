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
        public static SQLInterfaceEngine CreateInstance() => new();
    }
}
