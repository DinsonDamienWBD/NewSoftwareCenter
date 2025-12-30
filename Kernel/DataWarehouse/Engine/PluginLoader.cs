using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DataWarehouse.Contracts;

namespace DataWarehouse.Engine
{
    /// <summary>
    /// Load feature plugins
    /// </summary>
    public static class PluginLoader
    {
        /// <summary>
        /// Load plugins
        /// </summary>
        /// <param name="registry"></param>
        /// <param name="rootPath"></param>
        public static void LoadPlugins(PluginRegistry registry, string rootPath)
        {
            var pluginDir = Path.Combine(rootPath, "Plugins");
            if (!Directory.Exists(pluginDir)) return;

            var dlls = Directory.GetFiles(pluginDir, "DataWarehouse.*.dll");

            foreach (var dllPath in dlls)
            {
                try
                {
                    var assembly = Assembly.LoadFrom(dllPath);
                    var types = assembly.GetTypes()
                        .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                    foreach (var type in types)
                    {
                        var plugin = (IPlugin)Activator.CreateInstance(type)!;
                        registry.Register(plugin);
                    }
                }
                catch (Exception ex)
                {
                    // Log warning: "Failed to load plugin X"
                    Console.WriteLine($"Skipping {dllPath}: {ex.Message}");
                }
            }
        }
    }
}