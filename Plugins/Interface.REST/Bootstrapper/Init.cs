using System;
using Interface.REST.Engine;
using DataWarehouse.SDK.Contracts;

namespace Interface.REST.Bootstrapper
{
    public class RESTInterfacePlugin
    {
        public static PluginInfo PluginInfo => new()
        {
            Id = "interface.rest",
            Name = "REST API Interface",
            Version = new Version(1, 0, 0),
            Author = "DataWarehouse SDK",
            Description = "HTTP REST API with JSON request/response and OpenAPI documentation",
            Category = PluginCategory.Interface,
            Tags = new[] { "interface", "rest", "api", "http", "json", "openapi" }
        };

        public static RESTInterfaceEngine CreateInstance() => new RESTInterfaceEngine();
    }
}
