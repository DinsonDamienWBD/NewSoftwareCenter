using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using SoftwareCenter.Core.Diagnostics;
using SoftwareCenter.Core.Events;

namespace SoftwareCenter.Kernel.Services
{
    /// <summary>
    /// An event bus that uses the .NET dependency injection container to find and execute event handlers.
    /// </summary>
    public class EventBus : IEventBus
    {
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventBus"/> class.
        /// </summary>
        /// <param name="serviceProvider">The service provider used to resolve handlers.</param>
        public EventBus(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        /// <inheritdoc />
        public async Task Publish(IEvent @event, ITraceContext traceContext = null)
        {
            var eventType = @event.GetType();
            var handlerType = typeof(IEventHandler<>).MakeGenericType(eventType);

            // Get all registered handlers for this event type
            var handlers = _serviceProvider.GetServices(handlerType);

            if (!handlers.Any())
            {
                Console.WriteLine($"No handlers found for event type {eventType.Name}");
                return; // No handlers, nothing to do
            }

            var context = traceContext ?? new TraceContext();

            // Execute all handlers concurrently
            var tasks = handlers.Select(handler => (Task)((dynamic)handler).Handle((dynamic)@event, context));
            await Task.WhenAll(tasks);
        }
    }
}
