using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using SoftwareCenter.Core.Kernel;
using SoftwareCenter.Core.Modules;
using SoftwareCenter.Kernel.Contexts;
using SoftwareCenter.Kernel.Routing;

namespace SoftwareCenter.Kernel.Engine
{
    public class ModuleLoader
    {
        private readonly IKernel _kernel;
        private readonly HandlerRegistry _registry;

        public ModuleLoader(IKernel kernel, HandlerRegistry registry)
        {
            _kernel = kernel;
            _registry = registry;
        }

        public async Task LoadModulesAsync(string modulesRootPath)
        {
            if (!Directory.Exists(modulesRootPath)) return;

            foreach (var dir in Directory.GetDirectories(modulesRootPath))
            {
                await LoadSingleModuleAsync(dir);
            }
        }

        private async Task LoadSingleModuleAsync(string moduleDir)
        {
            var moduleName = Path.GetFileName(moduleDir);
            var dllPath = Path.Combine(moduleDir, $"{moduleName}.dll");

            if (!File.Exists(dllPath)) return;

            try
            {
                // 1. Isolation
                var loadContext = new ModuleLoadContext(dllPath);
                var assembly = loadContext.LoadFromAssemblyPath(dllPath);

                // 2. Find IModule
                var moduleType = assembly.GetTypes()
                    .FirstOrDefault(t => typeof(IModule).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                if (moduleType != null)
                {
                    // 3. Create Instance
                    var module = (IModule)Activator.CreateInstance(moduleType)!;

                    // 4. Initialize (Pass the Kernel)
                    // This is where the Module registers its Commands, Jobs, and Events.
                    await module.InitializeAsync(_kernel);

                    // Log success (In a real scenario, use EventBus or Logger)
                    Console.WriteLine($"[Kernel] Loaded Module: {moduleName} ({module.Version})");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Kernel] Failed to load {moduleName}: {ex.Message}");
            }
        }
    }
}