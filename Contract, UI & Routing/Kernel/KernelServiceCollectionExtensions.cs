using Microsoft.Extensions.DependencyInjection;
using SoftwareCenter.Kernel.Handlers;
using SoftwareCenter.Core.Commands;
using SoftwareCenter.Kernel.Services;
using SoftwareCenter.Core.Events;
using SoftwareCenter.Core.Errors;
using SoftwareCenter.Core.Modules;
using System.Reflection;
using System.Linq;
using System.Threading.Tasks;
using SoftwareCenter.Core.Attributes;
using SoftwareCenter.Core.Jobs;
using SoftwareCenter.Core.Routing;
using SoftwareCenter.Core.Discovery.Commands;
using SoftwareCenter.Core.Discovery;

namespace SoftwareCenter.Kernel
{
    public static class KernelServiceCollectionExtensions
    {
        public static IServiceCollection AddKernel(this IServiceCollection services)
        {
            // Register core Kernel services
            services.AddLogging();

            // Register the provider that will break the circular dependency
            services.AddSingleton<IErrorHandlerProvider, ErrorHandlerProvider>();

            // Register core Kernel services that have dependencies
            services.AddSingleton<IErrorHandler, DefaultErrorHandler>();
            services.AddSingleton<ISmartCommandRouter, SmartCommandRouter>();
            services.AddSingleton<ICommandBus, CommandBus>();
            services.AddSingleton<IEventBus, EventBus>();

            // Register other services
            services.AddSingleton<IServiceRegistry, ServiceRegistry>();
            services.AddSingleton<IServiceRoutingRegistry, ServiceRoutingRegistry>();
            services.AddSingleton<ModuleLoader>();
            services.AddSingleton<CommandFactory>();
            services.AddSingleton<RegistryManifestService>();
            services.AddSingleton<GlobalDataStore>();

            // Create a temporary collection of services to discover modules
            // This is safer than building a temporary service provider
            var tempServices = new ServiceCollection();
            tempServices.AddSingleton(services.BuildServiceProvider().GetRequiredService<IServiceRoutingRegistry>());
            tempServices.AddSingleton(services.BuildServiceProvider().GetRequiredService<IErrorHandler>());
            tempServices.AddSingleton(services.BuildServiceProvider().GetRequiredService<IServiceRegistry>()); // Add this line
            
            var tempServiceProvider = tempServices.BuildServiceProvider(); // Changed to a variable for reuse
            var moduleLoader = new ModuleLoader(
                tempServiceProvider.GetRequiredService<IErrorHandler>(),
                tempServiceProvider.GetRequiredService<IServiceRoutingRegistry>(),
                tempServiceProvider.GetRequiredService<IServiceRegistry>()
            );
            
            // Phase 1: Discover modules and let them add their services to the collection
            moduleLoader.ConfigureModuleServices(services);
            
            // Now that all services are registered, build the main provider to scan assemblies
            var provider = services.BuildServiceProvider();
            var serviceRoutingRegistry = provider.GetRequiredService<IServiceRoutingRegistry>();
            var errorHandler = provider.GetRequiredService<IErrorHandler>();

            // Now, discover handlers and validators from all loaded assemblies
            var assembliesToScan = moduleLoader.GetLoadedAssemblies();
            var commandHandlerInterface = typeof(ICommandHandler<,>);
            var fireAndForgetCommandHandlerInterface = typeof(ICommandHandler<>);
            var eventHandlerInterface = typeof(IEventHandler<>);
            var jobHandlerInterface = typeof(IJobHandler<>);
            var commandValidatorInterface = typeof(ICommandValidator<>);

            foreach (var assembly in assembliesToScan)
            {
                var owningModule = moduleLoader.GetLoadedModules().FirstOrDefault(m => m.Assembly == assembly);
                var moduleId = owningModule?.ModuleId ?? "Kernel";

                try
                {
                    var types = assembly.GetTypes().Where(t => !t.IsAbstract && !t.IsInterface);
                    foreach (var type in types)
                    {
                        // Register Handlers
                        var handlerPriority = type.GetCustomAttribute<HandlerPriorityAttribute>()?.Priority ?? 0;
                        var interfaces = type.GetInterfaces();
                        foreach (var i in interfaces)
                        {
                            if (i.IsGenericType)
                            {
                                var genericDef = i.GetGenericTypeDefinition();
                                if (genericDef == commandHandlerInterface || genericDef == fireAndForgetCommandHandlerInterface ||
                                    genericDef == eventHandlerInterface || genericDef == jobHandlerInterface)
                                {
                                    var contractType = i.GetGenericArguments()[0];
                                    // Registration is already done by modules, just need to update the routing registry
                                    serviceRoutingRegistry.RegisterHandler(contractType, type, i, handlerPriority, moduleId);
                                }
                            }
                        }

                        // Validators might also be registered by modules
                        var validatorInterfaces = type.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == commandValidatorInterface);
                        foreach (var validatorInterface in validatorInterfaces)
                        {
                            // Ensure validator is registered if not already
                             if (!services.Any(s => s.ServiceType == validatorInterface && s.ImplementationType == type))
                            {
                                services.AddTransient(validatorInterface, type);
                            }
                        }
                    }
                }
                catch (ReflectionTypeLoadException ex)
                {
                    errorHandler.HandleError(ex, new Core.Diagnostics.TraceContext(), $"Error loading types from assembly {assembly.FullName}");
                }
            }
            
            // Register default Kernel handlers
            services.AddTransient<DefaultLogCommandHandler>();
            serviceRoutingRegistry.RegisterHandler(typeof(LogCommand), typeof(DefaultLogCommandHandler), typeof(ICommandHandler<LogCommand>), -100, "Kernel");
            services.AddTransient<GetRegistryManifestCommandHandler>();
            serviceRoutingRegistry.RegisterHandler(typeof(GetRegistryManifestCommand), typeof(GetRegistryManifestCommandHandler), typeof(ICommandHandler<GetRegistryManifestCommand, RegistryManifest>), 0, "Kernel");

            // Register background services
            services.AddSingleton<JobSchedulerService>();
            services.AddHostedService(sp => sp.GetRequiredService<JobSchedulerService>());

            return services;
        }

        public static async Task UseKernel(this System.IServiceProvider serviceProvider)
        {
            var moduleLoader = serviceProvider.GetRequiredService<ModuleLoader>();
            await moduleLoader.InitializeModules(serviceProvider);
        }
    }
}