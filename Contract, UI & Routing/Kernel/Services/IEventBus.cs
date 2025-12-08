using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using SoftwareCenter.Core.Diagnostics;
using SoftwareCenter.Core.Events;

namespace SoftwareCenter.Kernel.Services
{
    /// <summary>
    /// Dispatches events to their respective handlers.
    /// </summary>
    public interface IEventBus
    {
        /// <summary>
        /// Publishes an event to all registered handlers.
        /// </summary>
        /// <param name="event">The event to publish.</param>
        /// <param name="traceContext">An optional existing trace context.</param>
        /// <returns>A task that represents the completion of the publishing process.</returns>
        Task Publish(IEvent @event, ITraceContext traceContext = null);
    }
}
