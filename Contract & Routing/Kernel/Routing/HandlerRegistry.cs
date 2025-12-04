using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SoftwareCenter.Core.Commands;
using SoftwareCenter.Core.Routing;

namespace SoftwareCenter.Kernel.Routing
{
    /// <summary>
    /// Implements Feature 2: The Handler Registry (Service Discovery).
    /// Acts as the dynamic catalog of all capabilities.
    /// Supports "Chain of Responsibility" via Priority Sorting.
    /// </summary>
    public class HandlerRegistry
    {
        // Internal storage class accessible to SmartRouter
        internal class HandlerEntry
        {
            public Func<ICommand, Task<IResult>> Handler { get; set; } = null!;
            public RouteMetadata Metadata { get; set; } = null!;
            public int Priority { get; set; }
        }

        // Dictionary: CommandID -> List of Handlers (Sorted by Priority High->Low)
        private readonly ConcurrentDictionary<string, List<HandlerEntry>> _registry = new();

        /// <summary>
        /// Registers a new command capability.
        /// </summary>
        /// <param name="commandId">The unique ID (e.g., "System.Log")</param>
        /// <param name="handler">The async delegate</param>
        /// <param name="metadata">Route details</param>
        /// <param name="priority">Higher number = Higher priority (Default 0)</param>
        public void Register(string commandId, Func<ICommand, Task<IResult>> handler, RouteMetadata metadata, int priority = 0)
        {
            var entry = new HandlerEntry
            {
                Handler = handler,
                Metadata = metadata,
                Priority = priority
            };

            _registry.AddOrUpdate(commandId,
                // New Key
                _ => new List<HandlerEntry> { entry },
                // Existing Key
                (_, list) =>
                {
                    lock (list)
                    {
                        list.Add(entry);
                        // Sort Descending: Highest priority first
                        list.Sort((a, b) => b.Priority.CompareTo(a.Priority));
                    }
                    return list;
                });
        }

        /// <summary>
        /// Retrieves the single best handler for a command.
        /// Returns internal Entry to allow SmartRouter to inspect Metadata.
        /// </summary>
        internal HandlerEntry? GetBestHandler(string commandId)
        {
            if (_registry.TryGetValue(commandId, out var list))
            {
                lock (list)
                {
                    return list.FirstOrDefault(); // First is highest priority
                }
            }
            return null;
        }

        /// <summary>
        /// Discovery API: Returns flat list of all available commands.
        /// Used by UI to generate menus or AI to know capabilities.
        /// </summary>
        public IEnumerable<RouteMetadata> GetRegistryManifest()
        {
            // We take the Metadata from the Best Handler for each ID.
            return _registry.Values
                .Select(list =>
                {
                    lock (list) return list.FirstOrDefault()?.Metadata;
                })
                .Where(m => m != null)
                .Select(m => m!) // Suppress nullable warning
                .DistinctBy(m => m.CommandId);
        }

        /// <summary>
        /// Removes handlers from a specific module (Hot-Swap support).
        /// </summary>
        public void UnregisterModule(string moduleName)
        {
            foreach (var key in _registry.Keys)
            {
                if (_registry.TryGetValue(key, out var list))
                {
                    lock (list)
                    {
                        list.RemoveAll(x => x.Metadata.SourceModule == moduleName);
                    }
                    // Clean up empty keys
                    if (list.Count == 0)
                    {
                        _registry.TryRemove(key, out _);
                    }
                }
            }
        }
    }
}