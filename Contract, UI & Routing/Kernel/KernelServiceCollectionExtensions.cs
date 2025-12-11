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
            services.AddSingleton<IErrorHandler, DefaultErrorHandler>();
            services.AddSingleton<IServiceRegistry, ServiceRegistry>();
            services.AddSingleton<IServiceRoutingRegistry, ServiceRoutingRegistry>();
            services.AddSingleton<ModuleLoader>();

            services.AddSingleton<ISmartCommandRouter, SmartCommandRouter>();
            services.AddSingleton<ICommandBus, CommandBus>();
            services.AddSingleton<IEventBus, EventBus>();
            services.AddSingleton<CommandFactory>();
            services.AddSingleton<RegistryManifestService>();
            services.AddSingleton<GlobalDataStore>();

            // Use a temporary service provider to run the first phase of module loading
            var tempServiceProvider = services.BuildServiceProvider();
            var moduleLoader = tempServiceProvider.GetRequiredService<ModuleLoader>();
            var serviceRoutingRegistry = tempServiceProvider.GetRequiredService<IServiceRoutingRegistry>();
            var errorHandler = tempServiceProvider.GetRequiredService<IErrorHandler>();

            // Phase 1: Discover modules and let them add their services to the collection
            moduleLoader.ConfigureModuleServices(services);

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
                                    services.AddTransient(type);
                                    serviceRoutingRegistry.RegisterHandler(contractType, type, i, handlerPriority, moduleId);
                                }
                            }
                        }

                        // Register Validators
                        var validatorInterfaces = type.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == commandValidatorInterface);
                        foreach(var validatorInterface in validatorInterfaces)
                        {
                            services.AddTransient(validatorInterface, type);
                        }
                    }
                }
                catch(ReflectionTypeLoadException ex)
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