using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SoftwareCenter.Core.Events;

namespace SoftwareCenter.Kernel.Services
{
    /// <summary>
    /// Implements Feature 3: The Event Bus (Pub/Sub).
    /// Asynchronous, loosely coupled messaging system.
    /// </summary>
    public class DefaultEventBus : IEventBus
    {
        // Topic -> List of Handlers
        private readonly ConcurrentDictionary<string, List<Func<IEvent, Task>>> _subscribers = new();

        public void Subscribe(string eventName, Func<IEvent, Task> handler)
        {
            _subscribers.AddOrUpdate(eventName,
                // Add new
                _ => new List<Func<IEvent, Task>> { handler },
                // Update existing
                (_, list) =>
                {
                    lock (list)
                    {
                        if (!list.Contains(handler))
                        {
                            list.Add(handler);
                        }
                    }
                    return list;
                });
        }

        public void Unsubscribe(string eventName, Func<IEvent, Task> handler)
        {
            if (_subscribers.TryGetValue(eventName, out var list))
            {
                lock (list)
                {
                    list.Remove(handler);
                }
            }
        }

        public async Task PublishAsync(IEvent systemEvent)
        {
            if (_subscribers.TryGetValue(systemEvent.Name, out var handlers))
            {
                List<Func<IEvent, Task>> handlersSnapshot;
                lock (handlers)
                {
                    handlersSnapshot = handlers.ToList(); // Snapshot to avoid modification during iteration
                }

                // Execute all handlers concurrently
                var tasks = handlersSnapshot.Select(h => SafeExecuteAsync(h, systemEvent));
                await Task.WhenAll(tasks);
            }
        }

        private async Task SafeExecuteAsync(Func<IEvent, Task> handler, IEvent systemEvent)
        {
            try
            {
                await handler(systemEvent);
            }
            catch (Exception ex)
            {
                // Critical: A subscriber crash must not crash the Publisher.
                // In a real system, we would log this internal failure to a dedicated error log.
                // Console.WriteLine($"Event Bus Error on topic {systemEvent.Name}: {ex.Message}");
            }
        }
    }
}