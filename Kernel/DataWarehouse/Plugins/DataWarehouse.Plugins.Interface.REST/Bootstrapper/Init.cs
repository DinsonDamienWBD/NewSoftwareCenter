using DataWarehouse.SDK.Contracts;
using DataWarehouse.Plugins.Interface.REST.Engine;

namespace DataWarehouse.Plugins.Interface.REST.Bootstrapper
{
    [PluginInfo(
        name: "REST API Interface",
        description: "HTTP REST API with JSON request/response and OpenAPI documentation",
        author: "DataWarehouse Team",
        version: "1.0.0",
        category: PluginCategory.Interface
    )]
    public class RESTInterfacePlugin
    {
        public static RESTInterfaceEngine CreateInstance() => new RESTInterfaceEngine();
    }
}
