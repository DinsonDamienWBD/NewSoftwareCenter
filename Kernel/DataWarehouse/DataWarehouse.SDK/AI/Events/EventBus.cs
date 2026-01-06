using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DataWarehouse.SDK.AI.Events
{
    /// <summary>
    /// Event bus for system-wide event distribution.
    /// Enables observability and proactive agent reactions.
    ///
    /// Used by:
    /// - Plugins: Emit events when operations occur
    /// - Proactive agents: Subscribe to events and react
    /// - Monitoring systems: Observe system behavior
    /// - AI Runtime: Learn from event patterns
    ///
    /// Event types:
    /// - Data events (BlobStored, BlobAccessed, BlobDeleted)
    /// - Performance events (SlowOperation, HighMemoryUsage)
    /// - Security events (UnauthorizedAccess, SuspiciousPattern)
    /// - System events (PluginLoaded, PluginUnloaded, Error)
    /// </summary>
    public class EventBus
    {
        private readonly Dictionary<string, List<IEventHandler>> _handlers = new();
        private readonly List<SystemEvent> _eventHistory = new();
        private readonly object _lock = new();
        private readonly int _maxHistorySize;

        public EventBus(int maxHistorySize = 10000)
        {
            _maxHistorySize = maxHistorySize;
        }

        /// <summary>
        /// Publishes an event to all subscribed handlers.
        /// Handlers execute asynchronously.
        /// </summary>
        /// <param name="event">Event to publish.</param>
        public async Task PublishAsync(SystemEvent @event)
        {
            if (@event == null)
                throw new ArgumentNullException(nameof(@event));

            @event.Timestamp = DateTime.UtcNow;
            @event.Id = Guid.NewGuid().ToString();

            // Record in history
            RecordEvent(@event);

            // Get handlers for this event type
            List<IEventHandler> handlers;
            lock (_lock)
            {
                _handlers.TryGetValue(@event.EventType, out var typeHandlers);
                _handlers.TryGetValue("*", out var wildcardHandlers); // Wildcard handlers get all events

                handlers = new List<IEventHandler>();
                if (typeHandlers != null) handlers.AddRange(typeHandlers);
                if (wildcardHandlers != null) handlers.AddRange(wildcardHandlers);
            }

            // Execute handlers asynchronously
            var tasks = handlers.Select(h => SafeHandleAsync(h, @event));
            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Subscribes a handler to an event type.
        /// </summary>
        /// <param name="eventType">Event type to subscribe to (or "*" for all events).</param>
        /// <param name="handler">Handler to invoke when event occurs.</param>
        public void Subscribe(string eventType, IEventHandler handler)
        {
            if (string.IsNullOrWhiteSpace(eventType))
                throw new ArgumentException("Event type cannot be empty");
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            lock (_lock)
            {
                if (!_handlers.ContainsKey(eventType))
                {
                    _handlers[eventType] = new List<IEventHandler>();
                }

                _handlers[eventType].Add(handler);
            }
        }

        /// <summary>
        /// Unsubscribes a handler from an event type.
        /// </summary>
        /// <param name="eventType">Event type to unsubscribe from.</param>
        /// <param name="handler">Handler to remove.</param>
        public void Unsubscribe(string eventType, IEventHandler handler)
        {
            lock (_lock)
            {
                if (_handlers.TryGetValue(eventType, out var handlers))
                {
                    handlers.Remove(handler);
                }
            }
        }

        /// <summary>
        /// Gets event history.
        /// </summary>
        /// <param name="eventType">Filter by event type (optional).</param>
        /// <param name="limit">Maximum events to return.</param>
        /// <returns>List of events.</returns>
        public List<SystemEvent> GetHistory(string? eventType = null, int limit = 100)
        {
            lock (_lock)
            {
                var query = _eventHistory.AsEnumerable();

                if (eventType != null)
                {
                    query = query.Where(e => e.EventType == eventType);
                }

                return query
                    .OrderByDescending(e => e.Timestamp)
                    .Take(limit)
                    .ToList();
            }
        }

        /// <summary>
        /// Clears event history.
        /// </summary>
        public void ClearHistory()
        {
            lock (_lock)
            {
                _eventHistory.Clear();
            }
        }

        /// <summary>
        /// Gets statistics about event types.
        /// </summary>
        /// <returns>Dictionary of event type → count.</returns>
        public Dictionary<string, int> GetEventStatistics()
        {
            lock (_lock)
            {
                return _eventHistory
                    .GroupBy(e => e.EventType)
                    .ToDictionary(g => g.Key, g => g.Count());
            }
        }

        /// <summary>
        /// Records event in history.
        /// </summary>
        private void RecordEvent(SystemEvent @event)
        {
            lock (_lock)
            {
                _eventHistory.Add(@event);

                // Trim history if too large
                if (_eventHistory.Count > _maxHistorySize)
                {
                    _eventHistory.RemoveAt(0);
                }
            }
        }

        /// <summary>
        /// Executes handler with exception handling.
        /// Prevents one handler's exception from affecting others.
        /// </summary>
        private async Task SafeHandleAsync(IEventHandler handler, SystemEvent @event)
        {
            try
            {
                await handler.HandleAsync(@event);
            }
            catch (Exception ex)
            {
                // Log error but don't propagate
                Console.WriteLine($"Event handler error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Base class for system events.
    /// </summary>
    public class SystemEvent
    {
        /// <summary>Unique event ID.</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>Event type (e.g., "BlobStored", "SlowOperation").</summary>
        public string EventType { get; set; } = string.Empty;

        /// <summary>When event occurred.</summary>
        public DateTime Timestamp { get; set; }

        /// <summary>Source of event (plugin ID, component name, etc.).</summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>Event data (specific to event type).</summary>
        public Dictionary<string, object> Data { get; init; } = new();

        /// <summary>Severity level.</summary>
        public EventSeverity Severity { get; set; } = EventSeverity.Info;
    }

    /// <summary>
    /// Event severity levels.
    /// </summary>
    public enum EventSeverity
    {
        Debug,
        Info,
        Warning,
        Error,
        Critical
    }

    /// <summary>
    /// Interface for event handlers.
    /// </summary>
    public interface IEventHandler
    {
        /// <summary>
        /// Handles an event.
        /// </summary>
        /// <param name="event">Event to handle.</param>
        Task HandleAsync(SystemEvent @event);
    }

    // =========================================================================
    // PREDEFINED EVENT TYPES
    // =========================================================================

    /// <summary>
    /// Event emitted when a blob is stored.
    /// </summary>
    public class BlobStoredEvent : SystemEvent
    {
        public BlobStoredEvent(string key, long sizeBytes, string storageProvider)
        {
            EventType = "BlobStored";
            Data["key"] = key;
            Data["sizeBytes"] = sizeBytes;
            Data["storageProvider"] = storageProvider;
        }
    }

    /// <summary>
    /// Event emitted when a blob is accessed/read.
    /// </summary>
    public class BlobAccessedEvent : SystemEvent
    {
        public BlobAccessedEvent(string key, string storageProvider, string? userId = null)
        {
            EventType = "BlobAccessed";
            Data["key"] = key;
            Data["storageProvider"] = storageProvider;
            if (userId != null) Data["userId"] = userId;
        }
    }

    /// <summary>
    /// Event emitted when a blob is deleted.
    /// </summary>
    public class BlobDeletedEvent : SystemEvent
    {
        public BlobDeletedEvent(string key, string storageProvider, string? userId = null)
        {
            EventType = "BlobDeleted";
            Data["key"] = key;
            Data["storageProvider"] = storageProvider;
            if (userId != null) Data["userId"] = userId;
            Severity = EventSeverity.Warning;
        }
    }

    /// <summary>
    /// Event emitted when an operation is slow.
    /// </summary>
    public class SlowOperationEvent : SystemEvent
    {
        public SlowOperationEvent(string capabilityId, double durationMs, double expectedMs)
        {
            EventType = "SlowOperation";
            Data["capabilityId"] = capabilityId;
            Data["durationMs"] = durationMs;
            Data["expectedMs"] = expectedMs;
            Data["slowdownFactor"] = durationMs / expectedMs;
            Severity = EventSeverity.Warning;
        }
    }

    /// <summary>
    /// Event emitted when memory usage is high.
    /// </summary>
    public class HighMemoryUsageEvent : SystemEvent
    {
        public HighMemoryUsageEvent(long usedBytes, long totalBytes, double percentUsed)
        {
            EventType = "HighMemoryUsage";
            Data["usedBytes"] = usedBytes;
            Data["totalBytes"] = totalBytes;
            Data["percentUsed"] = percentUsed;
            Severity = EventSeverity.Warning;
        }
    }

    /// <summary>
    /// Event emitted when a plugin is loaded.
    /// </summary>
    public class PluginLoadedEvent : SystemEvent
    {
        public PluginLoadedEvent(string pluginId, string pluginName, double loadTimeMs)
        {
            EventType = "PluginLoaded";
            Data["pluginId"] = pluginId;
            Data["pluginName"] = pluginName;
            Data["loadTimeMs"] = loadTimeMs;
            Severity = EventSeverity.Info;
        }
    }
}
