using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using SoftwareCenter.Core.Discovery;
using SoftwareCenter.Core.Events;
using SoftwareCenter.Core.Jobs;
using SoftwareCenter.Core.Routing;
using SoftwareCenter.Core.Commands;

namespace SoftwareCenter.Kernel.Services
{
    /// <summary>
    /// Responsible for generating the runtime capability manifest.
    /// </summary>
    public class RegistryManifestService
    {
        private readonly ModuleLoader _moduleLoader;
        private readonly Dictionary<Assembly, XmlDocumentationParser> _xmlParsers = new Dictionary<Assembly, XmlDocumentationParser>();

        /// <summary>
        /// Initializes a new instance of the <see cref="RegistryManifestService"/> class.
        /// </summary>
        /// <param name="moduleLoader">The module loader used to discover capabilities.</param>
        public RegistryManifestService(ModuleLoader moduleLoader)
        {
            _moduleLoader = moduleLoader;
        }

        /// <summary>
        /// Generates a manifest of all currently discovered capabilities.
        /// </summary>
        /// <returns>A <see cref="RegistryManifest"/> object.</returns>
        public RegistryManifest GenerateManifest()
        {
            var capabilities = new List<CapabilityDescriptor>();

            // Discover all handlers (Commands, Events, Jobs)
            var handlers = _moduleLoader.GetLoadedModules().SelectMany(m => m.Handlers);
            foreach (var handlerInfo in handlers)
            {
                var assembly = handlerInfo.ContractType.Assembly;
                if (!_xmlParsers.ContainsKey(assembly))
                {
                    XmlDocumentationParser.TryCreateForAssembly(assembly, out var parser);
                    _xmlParsers[assembly] = parser; // Cache it, even if null
                }

                var xmlParser = _xmlParsers[assembly];
                var description = xmlParser?.GetTypeSummary(handlerInfo.ContractType) ?? string.Empty;
                var status = !string.IsNullOrEmpty(description) ? CapabilityStatus.Available : CapabilityStatus.MetadataMissing;

                List<ParameterDescriptor> parameters = new List<ParameterDescriptor>();
                if (xmlParser != null)
                {
                    // For commands, events, jobs, parameters are typically in the constructor
                    var constructor = handlerInfo.ContractType.GetConstructors()
                        .OrderByDescending(c => c.GetParameters().Length)
                        .FirstOrDefault();
                    if (constructor != null)
                    {
                        parameters.AddRange(xmlParser.GetConstructorParameters(constructor));
                    }
                }

                CapabilityType capabilityType;
                if (typeof(ICommand).IsAssignableFrom(handlerInfo.ContractType))
                {
                    capabilityType = CapabilityType.Command;
                }
                else if (typeof(IEvent).IsAssignableFrom(handlerInfo.ContractType))
                {
                    capabilityType = CapabilityType.Event;
                }
                else if (typeof(IJob).IsAssignableFrom(handlerInfo.ContractType))
                {
                    capabilityType = CapabilityType.Job;
                }
                else
                {
                    capabilityType = CapabilityType.Service; // Default or unknown handler type
                }

                // Clean up name by removing "Command", "Event", or "Job" suffix if present
                var cleanedName = Regex.Replace(handlerInfo.ContractType.Name, "(Command|Event|Job)$", string.Empty);

                var descriptor = new CapabilityDescriptor(
                    name: cleanedName,
                    description: description,
                    type: capabilityType,
                    status: status,
                    priority: handlerInfo.Priority, // Pass the priority
                    contractTypeName: handlerInfo.ContractType.FullName,
                    handlerTypeName: handlerInfo.HandlerType.FullName,
                    owningModuleId: handlerInfo.OwningModuleId,
                    parameters: parameters
                );
                capabilities.Add(descriptor);
            }

            // Discover API Endpoints
            //var apiEndpoints = _moduleLoader.GetDiscoveredApiEndpoints();
            //foreach (var apiEndpoint in apiEndpoints)
            //{
            //    var assembly = apiEndpoint.GetType().Assembly;
            //    if (!_xmlParsers.ContainsKey(assembly))
            //    {
            //        XmlDocumentationParser.TryCreateForAssembly(assembly, out var parser);
            //        _xmlParsers[assembly] = parser;
            //    }
            //    var xmlParser = _xmlParsers[assembly];
            //    var description = xmlParser?.GetTypeSummary(apiEndpoint.GetType()) ?? apiEndpoint.Description ?? string.Empty; // Use explicit description if available
            //    var status = !string.IsNullOrEmpty(description) ? CapabilityStatus.Available : CapabilityStatus.MetadataMissing;

            //    // API endpoints might have method parameters, but for now, we'll just describe the endpoint itself.
            //    var parameters = new List<ParameterDescriptor>
            //    {
            //        new ParameterDescriptor("HttpMethod", typeof(string).FullName, "The HTTP method (GET, POST, etc.)"),
            //        new ParameterDescriptor("Path", typeof(string).FullName, "The API endpoint path")
            //    };


            //    var descriptor = new CapabilityDescriptor(
            //        name: $"{apiEndpoint.HttpMethod} {apiEndpoint.Path}",
            //        description: description,
            //        type: CapabilityType.ApiEndpoint,
            //        status: status,
            //        priority: 0, // API Endpoints don't currently have a priority attribute
            //        contractTypeName: typeof(IApiEndpoint).FullName,
            //        handlerTypeName: apiEndpoint.GetType().FullName,
            //        owningModuleId: apiEndpoint.OwningModuleId,
            //        parameters: parameters
            //    );
            //    capabilities.Add(descriptor);
            //}

            return new RegistryManifest(capabilities);
        }
    }
}
