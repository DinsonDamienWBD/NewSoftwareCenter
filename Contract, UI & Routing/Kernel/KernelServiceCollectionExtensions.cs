using Microsoft.Extensions.DependencyInjection;
using SoftwareCenter.Kernel.Handlers;
using SoftwareCenter.Core.Discovery.Commands;
using SoftwareCenter.Core.Commands; // Added for ICommandValidator
using SoftwareCenter.Core.Discovery;
using SoftwareCenter.Kernel.Services;
using SoftwareCenter.Core.Events;
using System.Reflection;
using System.Linq;
using System.Collections.Generic; // Added for List
using Microsoft.Extensions.Logging; // Added for logging services

namespace SoftwareCenter.Kernel
{
    public static class KernelServiceCollectionExtensions
    {
        public static IServiceCollection AddKernel(this IServiceCollection services)
        {
            // Register core Kernel services first
            var moduleLoader = new ModuleLoader();
            services.AddSingleton(moduleLoader);

            // CommandFactory is still needed to map command names to types for the API
            services.AddSingleton<CommandFactory>(); 
            services.AddSingleton<RegistryManifestService>();
            services.AddSingleton<GlobalDataStore>();
            
            // Register Smart Router components
            var serviceRoutingRegistry = new ServiceRoutingRegistry();
            services.AddSingleton<IServiceRoutingRegistry>(serviceRoutingRegistry);
            services.AddSingleton<ISmartCommandRouter, SmartCommandRouter>();

            // The CommandBus now relies on the SmartCommandRouter
            services.AddSingleton<ICommandBus, CommandBus>(); 
            services.AddSingleton<IEventBus, EventBus>();

            // Enable logging within the Kernel
            services.AddLogging();

            // Discover and configure modules
            var discoveredModules = moduleLoader.GetDiscoveredModules();
            foreach (var module in discoveredModules)
            {
                module.ConfigureServices(services);
            }

            // Register default Kernel handlers (can be overridden by modules)
            // The SmartCommandRouter will handle the resolution, so just register the concrete type for DI.
            // Also, populate the registry with this default handler
            services.AddTransient<GetRegistryManifestCommandHandler>();
            serviceRoutingRegistry.RegisterHandler(
                typeof(GetRegistryManifestCommand), 
                typeof(GetRegistryManifestCommandHandler), 
                typeof(ICommandHandler<GetRegistryManifestCommand, RegistryManifest>),
                0, // Default priority
                Assembly.GetExecutingAssembly().GetName().Name);


            // Dynamically register all discovered handlers and populate the routing registry
            var handlers = moduleLoader.GetDiscoveredHandlers();
            foreach (var handler in handlers)
            {
                // Register the concrete handler type with DI so the SmartCommandRouter can resolve it
                services.AddTransient(handler.HandlerType);
                
                // Register the handler with the routing registry
                serviceRoutingRegistry.RegisterHandler(
                    handler.ContractType,
                    handler.HandlerType,
                    handler.InterfaceType,
                    handler.Priority,
                    handler.OwningModuleId);
            }

            // Dynamically register all discovered command validators
            var assembliesToScan = moduleLoader.GetLoadedAssemblies();
            var commandValidatorInterface = typeof(ICommandValidator<>);
            foreach (var assembly in assembliesToScan)
            {
                var validatorTypes = assembly.GetTypes()
                    .Where(t => !t.IsAbstract && !t.IsInterface && t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == commandValidatorInterface));

                foreach (var validatorType in validatorTypes)
                {
                    var genericValidatorInterface = validatorType.GetInterfaces().First(i => i.IsGenericType && i.GetGenericTypeDefinition() == commandValidatorInterface);
                    services.AddTransient(genericValidatorInterface, validatorType);
                }
            }

            return services;
        }
    }
}
