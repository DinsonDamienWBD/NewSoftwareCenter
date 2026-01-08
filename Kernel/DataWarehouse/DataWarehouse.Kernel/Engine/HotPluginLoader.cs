using DataWarehouse.SDK.Contracts;
using DataWarehouse.SDK.Services;
using System.Reflection;
using System.Runtime.Loader;

namespace DataWarehouse.Kernel.Engine
{
    /// <summary>
    /// Hot plugin loader
    /// </summary>
    /// <param name="registry"></param>
    /// <param name="context"></param>
    public class HotPluginLoader(PluginRegistry registry, IKernelContext context)
    {
        private readonly PluginRegistry _registry = registry;
        private readonly IKernelContext _context = context;

        /// <summary>
        /// Load plugins from folder
        /// </summary>
        /// <param name="directory"></param>
        public void LoadPluginsFrom(string directory)
        {
            if (!Directory.Exists(directory)) return;

            foreach (var dll in Directory.GetFiles(directory, "*.dll", SearchOption.AllDirectories))
            {
                // Skip Kernel/SDK dlls to avoid collisions
                if (dll.Contains("DataWarehouse.Kernel") || dll.Contains("DataWarehouse.SDK")) continue;

                try
                {
                    LoadPluginDll(dll);
                }
                catch (Exception ex)
                {
                    _context.LogError($"Failed to load {Path.GetFileName(dll)}", ex);
                }
            }
        }

        private void LoadPluginDll(string path)
        {
            var loadContext = new PluginLoadContext(path);
            var assembly = loadContext.LoadFromAssemblyPath(path);

            foreach (var type in assembly.GetTypes())
            {
                if (typeof(IPlugin).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                {
                    var plugin = (IPlugin)Activator.CreateInstance(type)!;

                    // Use handshake protocol
                    var request = new HandshakeRequest
                    {
                        KernelId = Guid.NewGuid().ToString(),
                        ProtocolVersion = "1.0",
                        Timestamp = DateTime.UtcNow,
                        Mode = _context.Mode,
                        RootPath = _context.RootPath,
                        AlreadyLoadedPlugins = _registry.GetAllDescriptors()
                    };

                    var response = plugin.OnHandshakeAsync(request).GetAwaiter().GetResult();

                    _registry.Register(plugin);
                    _context.LogInfo($"Loaded Plugin: {response.PluginId} v{response.Version} ({response.State})");
                }
            }
        }

        private class PluginLoadContext(string pluginPath) : AssemblyLoadContext(pluginPath, true)
        {
            protected override Assembly? Load(AssemblyName assemblyName)
            {
                // Defer to default context for SDK/Shared types
                if (assemblyName.Name != null && assemblyName.Name.Contains("DataWarehouse.SDK")) return null;

                // Try load dependency from local folder
                string? folder = Path.GetDirectoryName(Name);
                if (folder != null)
                {
                    string local = Path.Combine(folder, assemblyName.Name + ".dll");
                    if (File.Exists(local)) return LoadFromAssemblyPath(local);
                }
                return null;
            }
        }
    }
}