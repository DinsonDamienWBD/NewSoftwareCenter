using DataWarehouse.SDK.Attributes;
using DataWarehouse.SDK.Contracts;
using System.Collections.Concurrent;
using System.Reflection;

namespace DataWarehouse.SDK.Services
{
    /// <summary>
    /// The Central Nervous System of the Microkernel.
    /// Manages the lifecycle, indexing, and intelligent retrieval of all loaded plugins.
    /// Supports Suitability Scoring based on OperatingMode.
    /// </summary>
    public class PluginRegistry
    {
        /// <summary>
        /// Plugin registration entry containing both the instance and its metadata.
        /// </summary>
        private class PluginEntry
        {
            public IPlugin Instance { get; init; } = null!;
            public HandshakeResponse Response { get; init; } = null!;
        }

        // Primary Store: Map PluginID -> Plugin Entry
        private readonly ConcurrentDictionary<string, PluginEntry> _plugins = new(StringComparer.OrdinalIgnoreCase);

        // Type Index: Map InterfaceType -> List of PluginIDs
        private readonly ConcurrentDictionary<Type, List<string>> _typeIndex = new();

        private OperatingMode _currentMode = OperatingMode.Laptop; // Default

        /// <summary>
        /// Sets the operating mode for intelligent resolution.
        /// </summary>
        /// <param name="mode">The detected mode.</param>
        public void SetOperatingMode(OperatingMode mode)
        {
            _currentMode = mode;
        }

        /// <summary>
        /// Registers a plugin and indexes it by all its implemented interfaces.
        /// </summary>
        /// <param name="plugin">The initialized plugin instance.</param>
        /// <param name="response">The handshake response containing plugin metadata.</param>
        public void Register(IPlugin plugin, HandshakeResponse response)
        {
            ArgumentNullException.ThrowIfNull(plugin);
            ArgumentNullException.ThrowIfNull(response);

            var entry = new PluginEntry
            {
                Instance = plugin,
                Response = response
            };

            // 1. Store by ID
            _plugins[response.PluginId] = entry;

            // 2. Index by Interfaces
            var interfaces = plugin.GetType().GetInterfaces();
            foreach (var iface in interfaces)
            {
                // We only care about IPlugin derivatives (like IStorageProvider, IMetadataIndex)
                if (typeof(IPlugin).IsAssignableFrom(iface) && iface != typeof(IPlugin))
                {
                    _typeIndex.AddOrUpdate(iface,
                        addValueFactory: _ => [response.PluginId],
                        updateValueFactory: (_, list) =>
                        {
                            lock (list) { if (!list.Contains(response.PluginId)) list.Add(response.PluginId); }
                            return list;
                        });
                }
            }
        }

        /// <summary>
        /// Retrieves the BEST matching plugin for the current environment.
        /// </summary>
        /// <typeparam name="T">The plugin interface.</typeparam>
        /// <returns>The winner of the priority contest.</returns>
        public T? GetPlugin<T>() where T : class, IPlugin
        {
            var candidates = GetPlugins<T>();
            if (!candidates.Any()) return null;

            // 1. Score Candidates
            var scored = candidates.Select(p => new
            {
                Plugin = p,
                Score = CalculateScore(p)
            });

            // 2. Pick Winner (Highest Score)
            return scored.OrderByDescending(x => x.Score).First().Plugin;
        }

        /// <summary>
        /// [FIX] Retrieves a specific plugin by ID.
        /// </summary>
        public T? GetPlugin<T>(string id) where T : class, IPlugin
        {
            if (_plugins.TryGetValue(id, out var entry) && entry.Instance is T typed)
            {
                return typed;
            }
            return null;
        }

        /// <summary>
        /// Retrieves all plugins of a given type.
        /// </summary>
        public IEnumerable<T> GetPlugins<T>() where T : class, IPlugin
        {
            var type = typeof(T);
            if (_typeIndex.TryGetValue(type, out var ids))
            {
                lock (ids)
                {
                    foreach (var id in ids)
                    {
                        if (_plugins.TryGetValue(id, out var entry) && entry.Instance is T typed)
                            yield return typed;
                    }
                }
            }
            else
            {
                // Fallback scan
                foreach (var entry in _plugins.Values)
                {
                    if (entry.Instance is T typed) yield return typed;
                }
            }
        }

        /// <summary>
        /// Calculates the suitability score for a plugin based on current mode and attributes.
        /// </summary>
        private int CalculateScore(IPlugin plugin)
        {
            var type = plugin.GetType();
            var attr = type.GetCustomAttribute<PluginPriorityAttribute>();

            int score = 0;

            if (attr != null)
            {
                // Base priority
                score += attr.Priority;

                // Bonus for matching environment
                if (attr.OptimizedFor == _currentMode)
                {
                    score += 100; // Massive boost for correct environment
                }
            }

            return score;
        }

        /// <summary>
        /// Returns all registered IDs.
        /// </summary>
        public IEnumerable<string> GetAllPluginIds() => _plugins.Keys;

        /// <summary>
        /// Gets descriptors of all registered plugins for dependency checking.
        /// Used by HandshakePluginLoader to build HandshakeRequest.
        /// </summary>
        public List<PluginDescriptor> GetAllDescriptors()
        {
            var descriptors = new List<PluginDescriptor>();

            foreach (var entry in _plugins.Values)
            {
                var type = entry.Instance.GetType();
                var interfaces = type.GetInterfaces()
                    .Where(i => typeof(IPlugin).IsAssignableFrom(i) && i != typeof(IPlugin))
                    .Select(i => i.Name)
                    .ToList();

                descriptors.Add(new PluginDescriptor
                {
                    PluginId = entry.Response.PluginId,
                    Name = entry.Response.Name,
                    Version = entry.Response.Version.ToString(),
                    Category = entry.Response.Category,
                    Interfaces = interfaces
                });
            }

            return descriptors;
        }

        /// <summary>
        /// Checks if any registered plugin implements a specific interface.
        /// Used for dependency checking during plugin loading.
        /// </summary>
        /// <param name="interfaceName">Name of the interface (e.g., "IMetadataIndex")</param>
        /// <returns>True if at least one plugin implements the interface.</returns>
        public bool HasInterface(string interfaceName)
        {
            // Check type index for exact match
            var match = _typeIndex.Keys.FirstOrDefault(t => t.Name == interfaceName);
            if (match != null)
            {
                return _typeIndex.TryGetValue(match, out var ids) && ids.Count > 0;
            }

            // Fallback: scan all plugins
            return _plugins.Values.Any(entry =>
                entry.Instance.GetType().GetInterfaces().Any(i => i.Name == interfaceName));
        }

        /// <summary>
        /// Gets the HandshakeResponse for a specific plugin by ID.
        /// </summary>
        /// <param name="pluginId">The plugin ID to look up.</param>
        /// <returns>The HandshakeResponse if found, null otherwise.</returns>
        public HandshakeResponse? GetPluginResponse(string pluginId)
        {
            return _plugins.TryGetValue(pluginId, out var entry) ? entry.Response : null;
        }
    }
}