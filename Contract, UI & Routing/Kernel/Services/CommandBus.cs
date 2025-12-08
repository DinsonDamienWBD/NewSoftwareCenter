using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using SoftwareCenter.Core.Commands;
using SoftwareCenter.Core.Events;
using SoftwareCenter.Kernel.Contracts;
using SoftwareCenter.Kernel.Data;

namespace SoftwareCenter.Kernel.Services
{
    public class CommandBus : IRouter
    {
        private readonly ServiceRegistry _registry;
        private readonly IEventBus _eventBus;

        public CommandBus(ServiceRegistry registry, IEventBus eventBus)
        {
            _registry = registry;
            _eventBus = eventBus;
        }

        public async Task<IResult> RouteAsync(ICommand command)
        {
            var stopwatch = Stopwatch.StartNew();

            TraceContext.StartNew();

            try
            {
                var entry = _registry.GetBestHandler(command.GetType().Name);

                if (entry == null)
                {
                    return Result.FromFailure($"Command '{command.GetType().Name}' not found in Registry.");
                }

                var handler = entry.Handler;
                var metadata = entry.Metadata;

                if (metadata.Status == RouteStatus.Obsolete)
                {
                    var msg = $"Blocked Obsolete Command: {command.GetType().Name}. {metadata.DeprecationMessage}";
                    return Result.FromFailure(msg);
                }

                if (metadata.Status == RouteStatus.Deprecated)
                {
                    _ = _eventBus.PublishAsync(new SystemEvent(
                        "System.Warning",
                        new Dictionary<string, object>
                        {
                            { "Message", $"Command '{command.GetType().Name}' is deprecated. {metadata.DeprecationMessage}" },
                            { "Source", metadata.SourceModule }
                        }
                    ));
                }

                IResult result;
                try
                {
                    result = await handler(command);
                }
                catch (Exception ex)
                {
                    return Result.FromFailure($"Kernel trapped error in '{command.GetType().Name}': {ex.Message}");
                }

                stopwatch.Stop();
                return result;
            }
            catch (Exception ex)
            {
                return Result.FromFailure($"Critical Router Error: {ex.Message}");
            }
        }
    }
}