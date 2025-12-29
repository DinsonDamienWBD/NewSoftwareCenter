using Core.Backend.Contracts;
using Core.Frontend.Contracts;
using Core.Modules.Contracts;
using System.Reflection;
using System.Runtime.Loader;

namespace Host.Services
{
    /// <summary>
    /// Custom Assembly Load Context for Modules
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    /// <param name="pluginPath"></param>
    public class ModuleLoadContext(string pluginPath) : AssemblyLoadContext(isCollectible: true)
    {
        private readonly AssemblyDependencyResolver _resolver = new(pluginPath);

        /// <summary>
        /// Load Assembly Override
        /// </summary>
        /// <param name="assemblyName"></param>
        /// <returns></returns>
        protected override Assembly? Load(AssemblyName assemblyName)
        {
            string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            if (assemblyPath != null)
            {
                return LoadFromAssemblyPath(assemblyPath);
            }
            return null; // Fallback to Default Context (Shared Assemblies like Core)
        }
    }

    /// <summary>
    /// Module Loader Service
    /// </summary>
    /// <param name="serviceProvider"></param>
    /// <param name="logger"></param>
    public partial class ModuleLoader(IServiceProvider serviceProvider, ILogger<ModuleLoader> logger)
    {
        private readonly IServiceProvider _serviceProvider = serviceProvider;
        private readonly ILogger<ModuleLoader> _logger = logger;
        private readonly List<IModule> _activeModules = [];

        // Track Contexts to allow Unloading
        private readonly Dictionary<string, ModuleLoadContext> _loadedContexts = [];

        /// <summary>
        /// Load Modules from the Modules folder
        /// </summary>
        /// <param name="backend"></param>
        /// <param name="frontend"></param>
        /// <returns></returns>
        public async Task LoadModulesAsync(IBackendRegistry backend, IFrontendRegistry frontend)
        {
            var modulesDir = Path.Combine(AppContext.BaseDirectory, "Modules");
            if (!Directory.Exists(modulesDir)) return;

            foreach (var dir in Directory.GetDirectories(modulesDir))
            {
                var moduleName = Path.GetFileName(dir);
                var dllPath = Path.Combine(dir, $"{moduleName}.dll");

                if (File.Exists(dllPath))
                {
                    try
                    {
                        // 1. Create Isloated Context
                        var context = new ModuleLoadContext(dllPath);
                        var assembly = context.LoadFromAssemblyPath(dllPath);

                        // 2. Find IModule
                        var moduleType = assembly.GetTypes().FirstOrDefault(t => typeof(IModule).IsAssignableFrom(t) && !t.IsInterface);
                        if (moduleType != null)
                        {
                            var module = (IModule)Activator.CreateInstance(moduleType)!;

                            // 3. Initialize
                            await module.InitializeAsync(new ModuleContext { ModulePath = dir });
                            module.Register(backend, frontend);
                            await module.StartAsync(CancellationToken.None);

                            _loadedContexts[module.ModuleId] = context;
                            _logger.LogInformation("Loaded Module: {ModuleName}", module.ModuleName);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to load {ModuleName}", moduleName);
                    }
                }
            }
        }

        private async Task TryLoadModule(string dllPath, IBackendRegistry backend, IFrontendRegistry frontend)
        {
            try
            {
                // A. Load Assembly
                var context = new AssemblyLoadContext(Path.GetFileNameWithoutExtension(dllPath), isCollectible: true);
                var assembly = context.LoadFromAssemblyPath(dllPath);

                // B. Find IModule implementation
                var moduleType = assembly.GetTypes()
                    .FirstOrDefault(t => typeof(IModule).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                if (moduleType == null) return; // Not a module DLL

                var module = (IModule)Activator.CreateInstance(moduleType)!;

                LogModuleFound(module.ModuleName, module.ModuleId);

                // --- THE HANDSHAKE ---

                // Step 1: Initialize
                var ctx = new ModuleContext
                {
                    ModulePath = Path.GetDirectoryName(dllPath)!,
                    // We allow the module to register internal services to a child scope if we wanted,
                    // but for V1 we pass null or a restricted collection. 
                    // To keep it simple, we skip service injection for V1 modules inside the Host container.
                    Services = null!
                };

                await module.InitializeAsync(ctx);

                // Step 2: Register
                module.Register(backend, frontend);

                // Step 3: Verify
                bool isHealthy = await module.VerifyAsync();
                if (!isHealthy)
                {
                    LogModuleVerificationFailed(module.ModuleName);
                    // Cleanup
                    await module.ShutdownAsync();
                    backend.UnregisterAll(module.ModuleId);
                    frontend.UnregisterAll(module.ModuleId);
                    context.Unload();
                    return;
                }

                // Step 4: Start
                await module.StartAsync(CancellationToken.None);

                _activeModules.Add(module);
                LogModuleStarted(module.ModuleName);
            }
            catch (Exception ex)
            {
                LogModuleLoadFailed(ex, dllPath);
            }
        }

        /// <summary>
        /// Shutdown all active modules
        /// </summary>
        /// <returns></returns>
        public async Task ShutdownAllAsync()
        {
            foreach (var module in _activeModules)
            {
                await module.ShutdownAsync();
            }
        }

        /// <summary>
        /// Unload a specific module by its ID
        /// </summary>
        /// <param name="moduleId"></param>
        /// <param name="backend"></param>
        /// <param name="frontend"></param>
        public void UnloadModule(string moduleId, IBackendRegistry backend, IFrontendRegistry frontend)
        {
            if (_loadedContexts.TryGetValue(moduleId, out var context))
            {
                // 1. Clean Registry (Crucial: Remove references to types in this ALC)
                backend.UnregisterAll(moduleId);
                frontend.UnregisterAll(moduleId);

                // 2. Unload
                context.Unload();
                _loadedContexts.Remove(moduleId);

                // 3. Force GC to prove unloading
                GC.Collect();
                GC.WaitForPendingFinalizers();
                _logger.LogInformation("Unloaded Module: {ModuleId}", moduleId);
            }
        }

        // =========================================================
        // Source Generated Logging (Fixes CA1873 & CA2254)
        // =========================================================

        [LoggerMessage(Level = LogLevel.Information, Message = "Found Module: {ModuleName} ({ModuleId})")]
        private partial void LogModuleFound(string moduleName, string moduleId);

        [LoggerMessage(Level = LogLevel.Error, Message = "Module {ModuleName} failed verification. Unloading.")]
        private partial void LogModuleVerificationFailed(string moduleName);

        [LoggerMessage(Level = LogLevel.Information, Message = "Module {ModuleName} Started Successfully.")]
        private partial void LogModuleStarted(string moduleName);

        [LoggerMessage(Level = LogLevel.Error, Message = "Failed to load module from {DllPath}")]
        private partial void LogModuleLoadFailed(Exception ex, string dllPath);
    }
}