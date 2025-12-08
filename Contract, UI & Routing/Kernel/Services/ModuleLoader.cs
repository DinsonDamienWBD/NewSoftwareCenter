using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using SoftwareCenter.Core.Modules;
using SoftwareCenter.Kernel.Contracts;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.Loader;

namespace SoftwareCenter.Kernel.Services
{
    public class ModuleLoader
    {
        private readonly IKernel _kernel;
        private readonly ServiceRegistry _registry;
        private readonly IServiceCollection _services;

        public ModuleLoader(IKernel kernel, ServiceRegistry registry, IServiceCollection services)
        {
            _kernel = kernel;
            _registry = registry;
            _services = services;
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
                var loadContext = new AssemblyLoadContext(moduleName, isCollectible: true);
                var assembly = loadContext.LoadFromAssemblyPath(dllPath);

                var moduleType = assembly.GetTypes()
                    .FirstOrDefault(t => typeof(IModule).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                if (moduleType != null)
                {
                    var module = (IModule)Activator.CreateInstance(moduleType)!;

                    module.Register(_services);

                    Console.WriteLine($"[Kernel] Loaded Module: {moduleName}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Kernel] Failed to load {moduleName}: {ex.Message}");
            }
        }
    }
}