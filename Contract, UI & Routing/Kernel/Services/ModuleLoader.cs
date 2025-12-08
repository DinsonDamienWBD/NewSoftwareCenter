using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader; // Added for AssemblyLoadContext
using SoftwareCenter.Core.Attributes;
using SoftwareCenter.Core.Commands;
using SoftwareCenter.Core.Events;
using SoftwareCenter.Core.Jobs;
using SoftwareCenter.Core.Modules;
using SoftwareCenter.Core.Routing;

namespace SoftwareCenter.Kernel.Services
{
    /// <summary>
    /// Discovers and loads modules and their capabilities from the filesystem.
    /// </summary>
    public class ModuleLoader
    {
        private readonly List<(Assembly Assembly, ModuleLoadContext Context)> _loadedModules = new List<(Assembly, ModuleLoadContext)>();
        private bool _modulesLoaded = false;

        public class DiscoveredHandler
        {
            public Type HandlerType { get; set; }
            public Type ContractType { get; set; } // ICommand, IEvent, IJob
            public Type InterfaceType { get; set; } // ICommandHandler<>, IEventHandler<>, IJobHandler<>
            public int Priority { get; set; } // The priority of this handler
            public string OwningModuleId { get; set; } // The module ID that registered this handler
        }

        public void LoadModulesFromDisk()
        {
            if (_modulesLoaded) return;

            var hostAssembly = Assembly.GetEntryAssembly();
            var rootPath = Path.GetDirectoryName(hostAssembly.Location);
            var modulesPath = Path.Combine(rootPath, "Modules");

            if (!Directory.Exists(modulesPath))
            {
                _modulesLoaded = true;
                return;
            }

            var moduleDirectories = Directory.GetDirectories(modulesPath);
            foreach (var dir in moduleDirectories)
            {
                var dirName = new DirectoryInfo(dir).Name;
                var dllPath = Path.Combine(dir, $"{dirName}.dll");

                if (File.Exists(dllPath))
                {
                    try
                    {
                        var loadContext = new ModuleLoadContext(dllPath); // Pass the path to the main assembly
                        var assembly = loadContext.LoadFromAssemblyName(new AssemblyName(dirName));
                        _loadedModules.Add((assembly, loadContext));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error loading module from {dllPath}: {ex.Message}");
                    }
                }
            }
            _modulesLoaded = true;
        }

        /// <summary>
        /// Returns a distinct list of all assemblies currently loaded by the ModuleLoader,
        /// including the entry assembly, the executing assembly, and all dynamically loaded module assemblies.
        /// </summary>
        /// <returns>A list of loaded Assemblies.</returns>
        public List<Assembly> GetLoadedAssemblies()
        {
            LoadModulesFromDisk(); // Ensure modules are loaded
            var assemblies = new List<Assembly> { Assembly.GetEntryAssembly(), Assembly.GetExecutingAssembly() };
            assemblies.AddRange(_loadedModules.Select(lm => lm.Assembly));
            return assemblies.Distinct().ToList();
        }

        public List<IModule> GetDiscoveredModules()
        {
            LoadModulesFromDisk();
            var moduleType = typeof(IModule);
            var instances = new List<IModule>();

            var assembliesToScan = GetLoadedAssemblies();

            foreach (var assembly in assembliesToScan)
            {
                var moduleImpls = assembly.GetTypes().Where(p => moduleType.IsAssignableFrom(p) && !p.IsInterface && !p.IsAbstract);
                foreach(var impl in moduleImpls)
                {
                    try
                    {
                        // Module instances are created by DI in ConfigureServices
                        instances.Add((IModule)Activator.CreateInstance(impl));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error instantiating module '{impl.Name}': {ex.Message}");
                    }
                }
            }
            return instances;
        }

        public List<DiscoveredHandler> GetDiscoveredHandlers()
        {
            LoadModulesFromDisk();
            var handlers = new List<DiscoveredHandler>();
            var assemblies = GetLoadedAssemblies();
            
            var commandHandlerInterface = typeof(ICommandHandler<,>);
            var fireAndForgetCommandHandlerInterface = typeof(ICommandHandler<>);
            var eventHandlerInterface = typeof(IEventHandler<>);
            var jobHandlerInterface = typeof(IJobHandler<>);

            foreach (var assembly in assemblies)
            {
                try
                {
                    var types = assembly.GetTypes();
                    foreach (var type in types)
                    {
                        if (type.IsAbstract || type.IsInterface) continue;

                        var handlerPriority = type.GetCustomAttribute<HandlerPriorityAttribute>()?.Priority ?? 0;
                        var owningModuleId = assembly.GetName().Name;

                        var interfaces = type.GetInterfaces();

                        // Command Handlers (with result)
                        foreach (var i in interfaces.Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == commandHandlerInterface))
                        {
                            handlers.Add(new DiscoveredHandler { HandlerType = type, ContractType = i.GetGenericArguments()[0], InterfaceType = i, Priority = handlerPriority, OwningModuleId = owningModuleId });
                        }

                        // Command Handlers (fire-and-forget)
                        foreach (var i in interfaces.Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == fireAndForgetCommandHandlerInterface))
                        {
                            handlers.Add(new DiscoveredHandler { HandlerType = type, ContractType = i.GetGenericArguments()[0], InterfaceType = i, Priority = handlerPriority, OwningModuleId = owningModuleId });
                        }

                        // Event Handlers
                        foreach (var i in interfaces.Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == eventHandlerInterface))
                        {
                            handlers.Add(new DiscoveredHandler { HandlerType = type, ContractType = i.GetGenericArguments()[0], InterfaceType = i, Priority = handlerPriority, OwningModuleId = owningModuleId });
                        }

                        // Job Handlers
                        foreach (var i in interfaces.Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == jobHandlerInterface))
                        {
                            handlers.Add(new DiscoveredHandler { HandlerType = type, ContractType = i.GetGenericArguments()[0], InterfaceType = i, Priority = handlerPriority, OwningModuleId = owningModuleId });
                        }
                    }
                }
                catch (ReflectionTypeLoadException) { /* Ignore */ }
                catch (Exception ex) { Console.WriteLine($"Error scanning assembly {assembly.FullName}: {ex.Message}"); }
            }
            return handlers;
        }

        public List<IApiEndpoint> GetDiscoveredApiEndpoints()
        {
            LoadModulesFromDisk();
            var endpoints = new List<IApiEndpoint>();
            var assemblies = GetLoadedAssemblies();
            var apiEndpointType = typeof(IApiEndpoint);

            foreach (var assembly in assemblies)
            {
                try
                {
                    var types = assembly.GetTypes();
                    foreach (var type in types)
                    {
                        if (type.IsAbstract || type.IsInterface) continue;
                        if (apiEndpointType.IsAssignableFrom(type))
                        {
                            try
                            {
                                // Instantiate IApiEndpoint implementations to get their properties
                                // These are assumed to be simple DTOs or stateless registrations
                                endpoints.Add((IApiEndpoint)Activator.CreateInstance(type));
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error instantiating IApiEndpoint '{type.Name}': {ex.Message}");
                            }
                        }
                    }
                }
                catch (ReflectionTypeLoadException) { /* Ignore */ }
                catch (Exception ex) { Console.WriteLine($"Error scanning assembly for API Endpoints {assembly.FullName}: {ex.Message}"); }
            }
            return endpoints;
        }

        /// <summary>
        /// Attempts to unload a module identified by its assembly.
        /// Note: Unloading requires all references to types within the ModuleLoadContext to be cleared.
        /// This is a complex operation and requires careful management of references.
        /// </summary>
        /// <param name="moduleId">The name of the module's assembly (e.g., "SoftwareCenter.AppManager").</param>
        public void UnloadModule(string moduleId)
        {
            var moduleToUnload = _loadedModules.FirstOrDefault(lm => lm.Assembly.GetName().Name == moduleId);
            if (moduleToUnload.Assembly != null)
            {
                try
                {
                    _loadedModules.Remove(moduleToUnload);
                    moduleToUnload.Context.Unload();
                    Console.WriteLine($"Module '{moduleId}' unloaded successfully.");
                    // Force garbage collection to collect the unloaded assembly.
                    // This is usually needed after Unload() for effective memory release.
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error unloading module '{moduleId}': {ex.Message}");
                }
            }
        }
    }
}
