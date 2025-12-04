using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SoftwareCenter.Core.Commands;
using SoftwareCenter.Core.Data;
using SoftwareCenter.Core.Diagnostics;
using SoftwareCenter.Core.Events;
using SoftwareCenter.Core.Logging;

namespace SoftwareCenter.Kernel.Services
{
    /// <summary>
    /// Implements Feature 6: Intelligent Logging Consumer.
    /// Observes Kernel traffic and broadcasts standardized LogEntry events.
    /// </summary>
    public class KernelLogger
    {
        private readonly IGlobalDataStore _dataStore;
        private readonly IEventBus _eventBus;

        // Cache logging setting to avoid DB hits on every log
        private bool? _verboseCache;

        public KernelLogger(IGlobalDataStore dataStore, IEventBus eventBus)
        {
            _dataStore = dataStore;
            _eventBus = eventBus;
        }

        /// <summary>
        /// Call this when settings change to invalidate cache.
        /// </summary>
        public void RefreshSettings()
        {
            _verboseCache = null;
        }

        /// <summary>
        /// Logs the outcome of a command execution.
        /// </summary>
        public async Task LogExecutionAsync(ICommand command, bool success, long durationMs, string? error = null)
        {
            // 1. Check Verbosity (Lazy Load)
            if (_verboseCache == null)
            {
                var setting = await _dataStore.RetrieveAsync<bool>("Settings.VerboseLogging");
                _verboseCache = setting?.Value ?? false; // Default to false
            }

            // 2. Construct Standard LogEntry
            var entry = new LogEntry
            {
                Timestamp = DateTime.UtcNow,
                Level = success ? LogLevel.Info : LogLevel.Error,
                Message = success
                    ? $"Command '{command.Name}' executed in {durationMs}ms."
                    : $"Command '{command.Name}' failed. {error}",
                Source = "Kernel", // Or command.History.FirstOrDefault()?.EntityId
                Category = "CommandExecution",
                TraceId = command.TraceId
            };

            // 3. Add Extended Data
            entry.ExtendedData["DurationMs"] = durationMs;
            entry.ExtendedData["CommandName"] = command.Name;

            if (error != null)
            {
                entry.ExtendedData["Error"] = error;
            }

            // 4. Verbose Mode: Attach Full History
            if (_verboseCache == true && command.History != null)
            {
                entry.ExtendedData["TraceHistory"] = command.History; // Logger module can serialize this
            }

            // 5. Publish
            // We publish a concrete "LogEvent" wrapper
            await _eventBus.PublishAsync(new LogEvent(entry));
        }

        // --- INTERNAL HELPER ---
        // Wraps the LogEntry into the IEvent envelope
        private class LogEvent : IEvent
        {
            public string Name => "System.Log.Internal";
            public DateTime Timestamp { get; } = DateTime.UtcNow;
            public string SourceId => "KernelLogger";
            public Guid? TraceId => TraceContext.CurrentTraceId;
            public Dictionary<string, object> Data { get; }

            public LogEvent(LogEntry entry)
            {
                Data = new Dictionary<string, object>
                {
                    { "LogEntry", entry }
                };
            }
        }
    }
}