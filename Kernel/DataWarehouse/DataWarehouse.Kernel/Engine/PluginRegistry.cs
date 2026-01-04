using DataWarehouse.SDK.Contracts;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace DataWarehouse.Kernel.Engine
{
    /// <summary>
    /// The Central Nervous System of the Microkernel.
    /// Manages the lifecycle, indexing, and retrieval of all loaded plugins.
    /// Supports O(1) lookups by ID and O(1) lookups by Interface Type.
    /// </summary>
    public class PluginRegistry
    {
        // Primary Store: Map PluginID -> Plugin Instance
        private readonly ConcurrentDictionary<string, IPlugin> _plugins = new(StringComparer.OrdinalIgnoreCase);

        // Type Index: Map InterfaceType -> List of PluginIDs
        // This allows extremely fast retrieval of "All IStorageProviders" without scanning the whole list.
        private readonly ConcurrentDictionary<Type, List<string>> _typeIndex = new();

        /// <summary>
        /// Registers a plugin and indexes it by all its implemented interfaces.
        /// </summary>
        /// <param name="plugin">The initialized plugin instance.</param>
        public void Register(IPlugin plugin)
        {
            if (plugin == null) throw new ArgumentNullException(nameof(plugin));

            // 1. Store by ID
            _plugins[plugin.Id] = plugin;

            // 2. Index by Interfaces
            // We scan the plugin's type and register it under every interface it implements.
            // This enables polymorphism support (e.g. asking for IPlugin, IFeaturePlugin, or IStorageProvider).
            var interfaces = plugin.GetType().GetInterfaces();
            foreach (var iface in interfaces)
            {
                // Optimization: Only index SDK interfaces to keep the index clean.
                // We assume all relevant contracts live in DataWarehouse.* namespaces.
                if (iface.Namespace?.StartsWith("DataWarehouse") == true)
                {
                    AddToIndex(iface, plugin.Id);
                }
            }
        }

        /// <summary>
        /// Thread-safe add to the type index.
        /// </summary>
        private void AddToIndex(Type type, string id)
        {
            _typeIndex.AddOrUpdate(type,
                addValueFactory: _ => new List<string> { id },
                updateValueFactory: (_, list) =>
                {
                    lock (list)
                    {
                        if (!list.Contains(id)) list.Add(id);
                    }
                    return list;
                });
        }

        /// <summary>
        /// Retrieves a specific plugin by its unique ID.
        /// </summary>
        public IPlugin? GetPlugin(string id)
        {
            _plugins.TryGetValue(id, out var p);
            return p;
        }

        /// <summary>
        /// Retrieves a typed plugin. 
        /// If ID is provided, returns that specific instance.
        /// If ID is null, returns the first registered instance of that type.
        /// </summary>
        public T? GetPlugin<T>(string? id = null) where T : class, IPlugin
        {
            if (!string.IsNullOrEmpty(id))
            {
                return GetPlugin(id) as T;
            }

            // If no ID is specified, return the first one we find (Service Locator pattern)
            return GetPlugins<T>().FirstOrDefault();
        }

        /// <summary>
        /// Retrieves all plugins that implement the specified interface.
        /// Uses the Type Index for O(1) lookup performance.
        /// </summary>
        public IEnumerable<T> GetPlugins<T>() where T : class, IPlugin
        {
            if (_typeIndex.TryGetValue(typeof(T), out var ids))
            {
                // Snapshot the list to avoid locking during iteration
                string[] idSnapshot;
                lock (ids)
                {
                    idSnapshot = ids.ToArray();
                }

                foreach (var id in idSnapshot)
                {
                    if (_plugins.TryGetValue(id, out var p) && p is T typed)
                    {
                        yield return typed;
                    }
                }
            }
            else
            {
                // Fallback: If type wasn't indexed (rare), scan all plugins.
                // This acts as a safety net.
                foreach (var p in _plugins.Values)
                {
                    if (p is T typed) yield return typed;
                }
            }
        }

        /// <summary>
        /// Helper: Retrieves the storage provider capable of handling the given URI scheme (e.g., "file", "s3").
        /// </summary>
        public IStorageProvider? GetStorage(string scheme)
        {
            return GetPlugins<IStorageProvider>()
                .FirstOrDefault(s => s.Scheme.Equals(scheme, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Helper: Retrieves all background feature plugins (for startup).
        /// </summary>
        public IEnumerable<IFeaturePlugin> GetFeatures()
        {
            return GetPlugins<IFeaturePlugin>();
        }

        /// <summary>
        /// Helper: Retrieves all generic middleware/transformation plugins.
        /// </summary>
        public IEnumerable<IDataTransformation> GetTransformations()
        {
            return GetPlugins<IDataTransformation>();
        }

        /// <summary>
        /// Returns all registered IDs.
        /// </summary>
        public IEnumerable<string> GetAllPluginIds() => _plugins.Keys;
    }
}