using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SoftwareCenter.Core.Commands;
using SoftwareCenter.Kernel.Data;

namespace SoftwareCenter.Kernel.Services
{
    public class ServiceRegistry
    {
        internal class HandlerEntry
        {
            public Func<ICommand, Task<IResult>> Handler { get; set; } = null!;
            public RouteMetadata Metadata { get; set; } = null!;
            public int Priority { get; set; }
        }

        private readonly ConcurrentDictionary<string, List<HandlerEntry>> _registry = new();

        public void Register(string commandId, Func<ICommand, Task<IResult>> handler, RouteMetadata metadata, int priority = 0)
        {
            var entry = new HandlerEntry
            {
                Handler = handler,
                Metadata = metadata,
                Priority = priority
            };

            _registry.AddOrUpdate(commandId,
                _ => new List<HandlerEntry> { entry },
                (_, list) =>
                {
                    lock (list)
                    {
                        list.Add(entry);
                        list.Sort((a, b) => b.Priority.CompareTo(a.Priority));
                    }
                    return list;
                });
        }

        internal HandlerEntry? GetBestHandler(string commandId)
        {
            if (_registry.TryGetValue(commandId, out var list))
            {
                lock (list)
                {
                    return list.FirstOrDefault();
                }
            }
            return null;
        }

        public IEnumerable<RouteMetadata> GetRegistryManifest()
        {
            var manifest = new List<RouteMetadata>();

            foreach (var handlerList in _registry.Values)
            {
                bool isFirst = true;
                lock (handlerList)
                {
                    foreach (var entry in handlerList)
                    {
                        var manifestEntry = new RouteMetadata
                        {
                            CommandId = entry.Metadata.CommandId,
                            Description = entry.Metadata.Description,
                            Version = entry.Metadata.Version,
                            Status = entry.Metadata.Status,
                            DeprecationMessage = entry.Metadata.DeprecationMessage,
                            SourceModule = entry.Metadata.SourceModule,
                            Priority = entry.Priority,
                            IsActiveSelection = isFirst
                        };
                        manifest.Add(manifestEntry);
                        isFirst = false;
                    }
                }
            }
            return manifest;
        }

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
                    if (list.Count == 0)
                    {
                        _registry.TryRemove(key, out _);
                    }
                }
            }
        }
    }
}