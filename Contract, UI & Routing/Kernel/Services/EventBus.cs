using SoftwareCenter.Core.Events;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SoftwareCenter.Kernel.Services
{
    /// <summary>
    /// A thread-safe, in-memory implementation of the IEventBus interface.
    /// </summary>
    public class EventBus : IEventBus
    {
        // Using ConcurrentDictionary for thread-safe additions and removals of event names.
        // The value is a list of handlers. Access to this list must be synchronized.
        private readonly ConcurrentDictionary<string, List<Func<IEvent, Task>>> _subscriptions = new();
        private readonly ILogger<EventBus> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventBus"/> class.
        /// </summary>
        /// <param name="logger">The kernel logger for logging all event bus activity.</param>
        public EventBus(ILogger<EventBus> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task PublishAsync(IEvent systemEvent)
        {
            ArgumentNullException.ThrowIfNull(systemEvent);
            _logger.LogInformation("Publishing event: {EventName}", systemEvent.GetType().Name);

            if (_subscriptions.TryGetValue(systemEvent.GetType().Name, out var handlers))
            {
                List<Func<IEvent, Task>> handlersSnapshot;
                lock (handlers)
                {
                    // Take a snapshot to avoid issues with collection modification during iteration.
                    handlersSnapshot = handlers.ToList();
                }

                // Execute handlers outside the lock to prevent deadlocks.
                foreach (var handler in handlersSnapshot)
                {
                    // We don't want one failing handler to stop others.
                    try
                    {
                        await handler(systemEvent);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Event handler for '{EventName}' failed.", systemEvent.GetType().Name);
                    }
                }
            }
        }

        /// <inheritdoc />
        public void Subscribe(string eventName, Func<IEvent, Task> handler)
        {
            var handlers = _subscriptions.GetOrAdd(eventName, _ => new List<Func<IEvent, Task>>());
            lock (handlers)
            {
                handlers.Add(handler);
            }
        }

        /// <inheritdoc />
        public void Unsubscribe(string eventName, Func<IEvent, Task> handler)
        {
            if (_subscriptions.TryGetValue(eventName, out var handlers))
            {
                lock (handlers)
                {
                    handlers.Remove(handler);

                    // If the list becomes empty after removal, we should remove the event name
                    // from the dictionary to prevent a memory leak of empty lists.
                    if (handlers.Count == 0)
                    {
                        _subscriptions.TryRemove(eventName, out _);
                    }
                }
            }
        }
    }
}