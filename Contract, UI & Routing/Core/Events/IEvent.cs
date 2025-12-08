using SoftwareCenter.Core.Diagnostics;
using System.Threading.Tasks;

namespace SoftwareCenter.Core.Events
{
    /// <summary>
    /// Marker interface for an event that has occurred in the system.
    /// Events are typically immutable and represent facts from the past.
    /// </summary>
    public interface IEvent
    {
    }

    /// <summary>
    /// Defines a handler for a specific type of <see cref="IEvent"/>.
    /// </summary>
    /// <typeparam name="TEvent">The type of event to be handled.</typeparam>
    public interface IEventHandler<in TEvent> where TEvent : IEvent
    {
        /// <summary>
        /// Handles an event asynchronously.
        /// </summary>
        /// <param name="event">The event to handle.</param>
        /// <param name="traceContext">The context for tracing the event processing.</param>
        /// <returns>A task that represents the asynchronous handling process.</returns>
        Task Handle(TEvent @event, ITraceContext traceContext);
    }
}
