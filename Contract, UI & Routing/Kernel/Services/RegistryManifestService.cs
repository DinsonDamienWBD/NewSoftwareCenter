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

                // First try to obtain a description for the contract type
                var description = xmlParser?.GetTypeSummary(handlerInfo.ContractType) ?? string.Empty;

                // If still empty, try the assembly-level summary as a fallback
                if (string.IsNullOrEmpty(description))
                {
                    description = xmlParser?.GetTypeSummary(handlerInfo.HandlerType) ?? string.Empty;
                }

                var status = !string.IsNullOrEmpty(description) ? CapabilityStatus.Available : CapabilityStatus.MetadataMissing;

                List<ParameterDescriptor> parameters = new List<ParameterDescriptor>();

                // For commands, events, jobs, parameters are typically in the constructor or factory method.
                if (xmlParser != null)
                {
                    // Prefer constructor parameter descriptions when available
                    var constructor = handlerInfo.ContractType.GetConstructors()
                        .OrderByDescending(c => c.GetParameters().Length)
                        .FirstOrDefault();
                    if (constructor != null)
                    {
                        parameters.AddRange(xmlParser.GetConstructorParameters(constructor));
                    }
                    else
                    {
                        // Fallback: look for a descriptive static "Create" method or single public method
                        var method = handlerInfo.ContractType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                            .OrderByDescending(m => m.GetParameters().Length)
                            .FirstOrDefault();
                        if (method != null)
                        {
                            foreach (var p in method.GetParameters())
                            {
                                parameters.Add(new ParameterDescriptor(p.Name, p.ParameterType.FullName, xmlParser.GetParameterDescription(method, p.Name)));
                            }
                        }
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

            // Discover API Endpoints (controller-based endpoints implementing IApiEndpoint)
            var apiEndpoints = _moduleLoader.GetDiscoveredApiEndpoints();
            foreach (var apiEndpoint in apiEndpoints)
            {
                var assembly = apiEndpoint.GetType().Assembly;
                if (!_xmlParsers.ContainsKey(assembly))
                {
                    XmlDocumentationParser.TryCreateForAssembly(assembly, out var parser);
                    _xmlParsers[assembly] = parser;
                }
                var xmlParser = _xmlParsers[assembly];
                var description = xmlParser?.GetTypeSummary(apiEndpoint.GetType()) ?? apiEndpoint.Description ?? string.Empty;
                var status = !string.IsNullOrEmpty(description) ? CapabilityStatus.Available : CapabilityStatus.MetadataMissing;

                // API endpoints might have route parameters; attempt to describe them via reflection on properties or method parameters.
                var parameters = new List<ParameterDescriptor>();
                try
                {
                    var method = apiEndpoint.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance).FirstOrDefault();
                    if (method != null)
                    {
                        foreach (var p in method.GetParameters())
                        {
                            var paramDesc = xmlParser != null ? xmlParser.GetParameterDescription(method, p.Name) : string.Empty;
                            parameters.Add(new ParameterDescriptor(p.Name, p.ParameterType.FullName, paramDesc));
                        }
                    }
                }
                catch
                {
                    // Ignore reflection errors here; this is best-effort metadata
                }

                var descriptor = new CapabilityDescriptor(
                    name: $"{apiEndpoint.HttpMethod} {apiEndpoint.Path}",
                    description: description,
                    type: CapabilityType.ApiEndpoint,
                    status: status,
                    priority: 0,
                    contractTypeName: typeof(IApiEndpoint).FullName,
                    handlerTypeName: apiEndpoint.GetType().FullName,
                    owningModuleId: apiEndpoint.OwningModuleId,
                    parameters: parameters
                );
                capabilities.Add(descriptor);
            }

            return new RegistryManifest(capabilities);
        }
    }
}
