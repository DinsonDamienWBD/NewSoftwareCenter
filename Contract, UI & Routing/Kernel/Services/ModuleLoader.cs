using Microsoft.Extensions.DependencyInjection;
using SoftwareCenter.Core.Attributes;
using SoftwareCenter.Core.Commands;
using SoftwareCenter.Core.Diagnostics;
using SoftwareCenter.Core.Errors;
using SoftwareCenter.Core.Events;
using SoftwareCenter.Core.Jobs;
using SoftwareCenter.Core.Modules;
using SoftwareCenter.Core.Routing;
using SoftwareCenter.Kernel.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging; // Added for ILogger

namespace SoftwareCenter.Kernel.Services
{
    /// <summary>
    /// Manages the discovery, loading, and lifecycle of modules.
    /// This class orchestrates a multi-phase loading process to integrate modules with the DI container.
    /// </summary>
    public class ModuleLoader
    {
        private readonly ILogger<ModuleLoader> _logger; // Changed from IErrorHandler
        private readonly IServiceRoutingRegistry _serviceRoutingRegistry;
        private readonly IServiceRegistry _serviceRegistry;
        private readonly List<IApiEndpoint> _discoveredEndpoints = new();
        private readonly Dictionary<string, ModuleInfo> _loadedModules = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="ModuleLoader"/> class.
        /// </summary>
        /// <param name="logger">The logger for module loading events.</param>
        /// <param name="serviceRoutingRegistry">The registry for command/event/job handlers.</param>
        /// <param name="serviceRegistry">The registry for general services.</param>
        public ModuleLoader(ILogger<ModuleLoader> logger, IServiceRoutingRegistry serviceRoutingRegistry, IServiceRegistry serviceRegistry)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceRoutingRegistry = serviceRoutingRegistry ?? throw new ArgumentNullException(nameof(serviceRoutingRegistry));
            _serviceRegistry = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));
        }


        /// <summary>
        /// Represents a handler (for a command, event, etc.) discovered within a module.
        /// </summary>
        public class DiscoveredHandler
        {
            /// <summary>
            /// Gets or sets the concrete type of the handler.
            /// </summary>
            public Type HandlerType { get; set; }
            /// <summary>
            /// Gets or sets the type of the contract (e.g., the command) that the handler processes.
            /// </summary>
            public Type ContractType { get; set; }
            /// <summary>
            /// Gets or sets the specific interface the handler implements (e.g., ICommandHandler<MyCommand>).
            /// </summary>
            public Type InterfaceType { get; set; }
            /// <summary>
            /// Gets or sets the priority of the handler.
            /// </summary>
            public int Priority { get; set; }
            /// <summary>
            /// Gets or sets the ID of the module that owns this handler.
            /// </summary>
            public string OwningModuleId { get; set; }
        }

        /// <summary>
        /// Phase 1 of module loading. Discovers modules on disk and calls their ConfigureServices method.
        /// This method should be called during the host's service configuration phase.
        /// </summary>
        /// <param name="services">The service collection to which modules will add their services.</param>
        public void ConfigureModuleServices(IServiceCollection services)
        {
            var hostAssembly = Assembly.GetEntryAssembly();
            var rootPath = Path.GetDirectoryName(hostAssembly.Location);
            var modulesPath = Path.Combine(rootPath, "Modules");

            if (!Directory.Exists(modulesPath))
            {
                return;
            }

            var moduleDirectories = Directory.GetDirectories(modulesPath);
            foreach (var dir in moduleDirectories)
            {
                var dirName = new DirectoryInfo(dir).Name;
                var dllPath = Path.Combine(dir, $"{dirName}.dll");
                if (File.Exists(dllPath))
                {
                    LoadModuleAndConfigureServices(dllPath, services);
                }
            }
        }

        /// <summary>
        /// Loads a single module assembly, finds its IModule implementation, and calls its ConfigureServices method.
        /// </summary>
        /// <param name="dllPath">The full path to the module's DLL.</param>
        /// <param name="services">The service collection for service registration.</param>
        private void LoadModuleAndConfigureServices(string dllPath, IServiceCollection services)
        {
            var assemblyName = AssemblyName.GetAssemblyName(dllPath);
            var moduleId = assemblyName.Name;

            if (_loadedModules.ContainsKey(moduleId))
            {
                _logger.LogWarning("Module '{ModuleId}' is already loaded. Skipping.", moduleId); // Changed from _errorHandler
                return;
            }

            try
            {
                var loadContext = new ModuleLoadContext(dllPath);
                var assembly = loadContext.LoadFromAssemblyName(assemblyName);

                var moduleType = assembly.GetTypes().FirstOrDefault(t => typeof(IModule).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                if (moduleType != null)
                {
                    // Create a temporary instance just to call ConfigureServices
                    var tempModuleInstance = (IModule)Activator.CreateInstance(moduleType);

                    // The module registers its services, including its own IModule implementation
                    tempModuleInstance.ConfigureServices(services);

                    var moduleInfo = new ModuleInfo(moduleId, assembly, loadContext);
                    _loadedModules.Add(moduleId, moduleInfo);
                    DiscoverAndRegisterHandlers(moduleInfo);
                    
                    _logger.LogInformation("Module '{ModuleId}' discovered and services configured.", moduleId); // Changed from _errorHandler
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading and configuring services for module from {DllPath}.", dllPath); // Changed from _errorHandler
            }
        }

        /// <summary>
        /// Phase 2 of module loading. Initializes all loaded modules by calling their Initialize method.
        /// This should be called after the service provider has been built.
        /// </summary>
        /// <param name="serviceProvider">The application's fully built service provider.</param>
        public async Task InitializeModules(IServiceProvider serviceProvider)
        {
            var modules = serviceProvider.GetServices<IModule>();
            var errorHandler = serviceProvider.GetRequiredService<IErrorHandler>(); // Resolve IErrorHandler here

            foreach (var module in modules)
            {
                ModuleInfo moduleInfo = null; // Declare and initialize moduleInfo to null

                try
                {
                    // Find the corresponding ModuleInfo to store the final, DI-managed instance
                    if (_loadedModules.TryGetValue(module.Id, out moduleInfo))
                    {
                        moduleInfo.Instance = module;
                        moduleInfo.State = ModuleState.Initializing;

                        await module.Initialize(serviceProvider);

                        moduleInfo.State = ModuleState.Initialized;
                        _logger.LogInformation("Module '{ModuleId}' initialized successfully.", module.Id);
                    }
                    else
                    {
                        errorHandler.HandleError(new InvalidOperationException($"Module {module.Id} was resolved from DI but not found in the loader's registry."), new TraceContext(), isCritical: false);
                    }
                }
                catch (Exception ex)
                {
                    errorHandler.HandleError(ex, new TraceContext(), $"Error initializing module '{module.Id}'.", isCritical: false);
                    if (moduleInfo != null) // Add null check before accessing moduleInfo.State
                    {
                        moduleInfo.State = ModuleState.Error;
                    }
                }
            }
        }

        /// <summary>
        /// Discovers and registers command, event, and job handlers from a module's assembly.
        /// </summary>
        /// <param name="moduleInfo">The information record for the module.</param>
        private void DiscoverAndRegisterHandlers(ModuleInfo moduleInfo)
        {
            var assembly = moduleInfo.Assembly;
            var moduleId = moduleInfo.ModuleId;

            var commandHandlerInterface = typeof(ICommandHandler<,>);
            var fireAndForgetCommandHandlerInterface = typeof(ICommandHandler<>);
            var eventHandlerInterface = typeof(IEventHandler<>);
            var jobHandlerInterface = typeof(IJobHandler<>);
            var apiEndpointInterface = typeof(IApiEndpoint);

            foreach (var type in assembly.GetTypes().Where(t => !t.IsAbstract && !t.IsInterface))
            {
                // 1. Discover Handlers
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
                            _serviceRoutingRegistry.RegisterHandler(contractType, type, i, handlerPriority, moduleId);
                            moduleInfo.Handlers.Add(new DiscoveredHandler { HandlerType = type, ContractType = contractType, InterfaceType = i, Priority = handlerPriority, OwningModuleId = moduleId });
                        }
                    }
                    else if (i == apiEndpointInterface) // For non-generic IApiEndpoint directly
                    {
                        // Handle direct implementation of IApiEndpoint
                        // This case is typically covered by the IsAssignableFrom check below, but including for clarity.
                    }
                }

                // 2. Discover API Endpoints (New Logic)
                if (apiEndpointInterface.IsAssignableFrom(type))
                {
                    try
                    {
                        // Instantiate to get metadata (assuming stateless/parameterless constructor for descriptors)
                        var endpointInstance = (IApiEndpoint)Activator.CreateInstance(type);
                        if (endpointInstance != null)
                        {
                            var prop = type.GetProperty(nameof(IApiEndpoint.OwningModuleId), BindingFlags.Public | BindingFlags.Instance);
                            if (prop != null && prop.CanWrite)
                            {
                                prop.SetValue(endpointInstance, moduleId);
                            }
                            _discoveredEndpoints.Add(endpointInstance);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to instantiate API endpoint '{TypeName}' in module '{ModuleId}'.", type.Name, moduleId); // Changed from _errorHandler
                    }
                }
            }
        }

        /// <summary>
        /// Unloads a module, releasing its assembly and unregistering its services.
        /// </summary>
        /// <param name="moduleId">The ID of the module to unload.</param>
        public void UnloadModule(string moduleId)
        {
            if (!_loadedModules.TryGetValue(moduleId, out var moduleInfo))
            {
                _logger.LogWarning("Module '{ModuleId}' not found for unloading.", moduleId); // Changed from _errorHandler
                return;
            }

            try
            {
                moduleInfo.State = ModuleState.Unloading;

                _serviceRoutingRegistry.UnregisterModuleHandlers(moduleId);
                _discoveredEndpoints.RemoveAll(e => e.OwningModuleId == moduleId);
                // Note: True DI unload is not simple. This relies on the routing registry blocking access.
                // For a full unload, the service provider would need to be rebuilt.

                _loadedModules.Remove(moduleId);
                moduleInfo.LoadContext.Unload();
                moduleInfo.State = ModuleState.Unloaded;

                _logger.LogInformation("Module '{ModuleId}' unloaded successfully.", moduleId); // Changed from _errorHandler

                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unloading module '{ModuleId}'.", moduleId);
                moduleInfo.State = ModuleState.Error; // Directly use the existing moduleInfo, which is in scope.
            }
        }

        /// <summary>
        /// Gets a list of all loaded assemblies, including the host and kernel.
        /// </summary>
        /// <returns>A distinct list of loaded assemblies.</returns>
        public List<Assembly> GetLoadedAssemblies()
        {
            var assemblies = new List<Assembly> { Assembly.GetEntryAssembly(), Assembly.GetExecutingAssembly() };
            assemblies.AddRange(_loadedModules.Values.Select(m => m.Assembly));
            return assemblies.Distinct().ToList();
        }
        
        /// <summary>
        /// Gets information about all currently loaded modules.
        /// </summary>
        /// <returns>An enumerable of <see cref="ModuleInfo"/>.</returns>
        public IEnumerable<ModuleInfo> GetLoadedModules() => _loadedModules.Values;

        /// <summary>
        /// Gets all discovered API endpoints from loaded modules.
        /// </summary>
        /// <returns>An enumerable of discovered <see cref="IApiEndpoint"/> instances.</returns>
        public IEnumerable<IApiEndpoint> GetDiscoveredApiEndpoints() => _discoveredEndpoints;
    }
}