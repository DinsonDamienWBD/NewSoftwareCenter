using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SoftwareCenter.Core.Commands;

namespace SoftwareCenter.Kernel.Services
{
    /// <summary>
    /// Discovers and provides a mapping from simple command names to their full .NET types.
    /// </summary>
    public class CommandFactory
    {
        private readonly Dictionary<string, Type> _commandMap;

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandFactory"/> class.
        /// </summary>
        /// <param name="moduleLoader">The module loader to get discovered assemblies from.</param>
        public CommandFactory(ModuleLoader moduleLoader)
        {
            _commandMap = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            
            // Ensure modules are loaded before scanning
            moduleLoader.LoadModulesFromDisk();

            var assemblies = new List<Assembly> { Assembly.GetEntryAssembly(), Assembly.GetExecutingAssembly() }
                .Concat(moduleLoader.GetDiscoveredModules().Select(m => m.GetType().Assembly))
                .Distinct();

            var commandInterface = typeof(ICommand);

            foreach (var assembly in assemblies)
            {
                var commandTypes = assembly.GetTypes()
                    .Where(t => commandInterface.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                foreach (var type in commandTypes)
                {
                    if (_commandMap.ContainsKey(type.Name))
                    {
                        // Handle potential conflicts, e.g., log a warning.
                        // For now, we'll let the last one loaded win.
                        Console.WriteLine($"Warning: Duplicate command name '{type.Name}' found. Overwriting with type from assembly '{assembly.FullName}'.");
                    }
                    _commandMap[type.Name.Replace("Command", "")] = type;
                }
            }
        }

        /// <summary>
        /// Gets the .NET type for a given simple command name.
        /// </summary>
        /// <param name="commandName">The simple name of the command (e.g., "GetRegistryManifest").</param>
        /// <returns>The command's type, or null if not found.</returns>
        public Type GetCommandType(string commandName)
        {
            _commandMap.TryGetValue(commandName, out var type);
            return type;
        }
    }
}
