using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using SoftwareCenter.Core.Commands;
using SoftwareCenter.Core.Diagnostics;
using SoftwareCenter.Core.Events;
using SoftwareCenter.Core.Routing;

namespace SoftwareCenter.Kernel.Routing
{
    /// <summary>
    /// Implements Feature 1: The Smart Router.
    /// Acts as the central "Traffic Cop" and "Exception Barrier".
    /// Delegates storage to HandlerRegistry.
    /// </summary>
    public class SmartRouter : IRouter
    {
        private readonly HandlerRegistry _registry;
        private readonly IEventBus _eventBus;

        // Dependency Injection: Router needs the Phonebook (Registry) and the Radio (EventBus)
        public SmartRouter(HandlerRegistry registry, IEventBus eventBus)
        {
            _registry = registry;
            _eventBus = eventBus;
        }

        public async Task<IResult> RouteAsync(ICommand command)
        {
            var stopwatch = Stopwatch.StartNew();

            // 1. Trace Context Management (Scope: AsyncLocal)
            // If the host passed a trace, use it. Otherwise, start a new one.
            if (command.TraceId == Guid.Empty)
            {
                TraceContext.StartNew();
            }
            else
            {
                TraceContext.CurrentTraceId = command.TraceId;
            }

            // Audit the entry
            command.History.Add(new TraceHop("Kernel", "Routing"));

            try
            {
                // 2. Registry Lookup
                var entry = _registry.GetBestHandler(command.Name);

                // 3. Fallback Logic (Basic)
                if (entry == null)
                {
                    // Fallback logic could go here (e.g. check legacy PowerShell maps)
                    // For now, strict failure.
                    return Result.FromFailure($"Command '{command.Name}' not found in Registry.");
                }

                var handler = entry.Handler;
                var metadata = entry.Metadata;

                // 4. Metadata Gates (The "Traffic Cop")

                // A. Obsolete Check (Blocker)
                if (metadata.Status == RouteStatus.Obsolete)
                {
                    var msg = $"Blocked Obsolete Command: {command.Name}. {metadata.DeprecationMessage}";
                    return Result.FromFailure(msg);
                }

                // B. Deprecation Check (Warning)
                if (metadata.Status == RouteStatus.Deprecated)
                {
                    // Fire-and-forget warning event
                    _ = _eventBus.PublishAsync(new SystemEvent(
                        "System.Warning",
                        new Dictionary<string, object>
                        {
                            { "Message", $"Command '{command.Name}' is deprecated. {metadata.DeprecationMessage}" },
                            { "Source", metadata.SourceModule }
                        }
                    ));
                }

                // 5. Middleware: Trace Injection for Logging
                // If the target is the Logging system, we must attach the invisible context 
                // so the Logger can serialize the history.
                if (command.Name == "System.Log" && command.Parameters != null)
                {
                    command.Parameters["TraceHistory"] = command.History;
                    command.Parameters["TraceId"] = TraceContext.CurrentTraceId;
                }

                // 6. Execution (Exception Barrier)
                IResult result;
                try
                {
                    // HAND OVER CONTROL TO MODULE
                    result = await handler(command);
                    result.History.Add(new TraceHop("Kernel", "Returning"));
                }
                catch (Exception ex)
                {
                    // 7. Safety Net: Prevent App Crash
                    command.History.Add(new TraceHop("Kernel", $"Crash: {ex.Message}"));
                    return Result.FromFailure($"Kernel trapped error in '{command.Name}': {ex.Message}");
                }

                stopwatch.Stop();
                return result;
            }
            catch (Exception ex)
            {
                // Catastrophic Router Failure
                return Result.FromFailure($"Critical Router Error: {ex.Message}");
            }
        }

        // --- INTERNAL HELPER FOR EVENTS ---
        private class SystemEvent : IEvent
        {
            public string Name { get; }
            public Dictionary<string, object> Data { get; }
            public DateTime Timestamp { get; } = DateTime.UtcNow;
            public string SourceId => "Kernel";
            public Guid? TraceId => TraceContext.CurrentTraceId;

            public SystemEvent(string name, Dictionary<string, object> data)
            {
                Name = name;
                Data = data;
            }
        }
    }
}