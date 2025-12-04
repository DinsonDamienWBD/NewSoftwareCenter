using System;
using System.Threading.Tasks;

namespace SoftwareCenter.Core.Events
{
    /// <summary>
    /// Defines the Pub/Sub mechanism.
    /// Implemented by the Kernel.
    /// </summary>
    public interface IEventBus
    {
        /// <summary>
        /// Broadcasts an event to all subscribers of that event name.
        /// </summary>
        /// <param name="systemEvent">The event envelope.</param>
        Task PublishAsync(IEvent systemEvent);

        /// <summary>
        /// Subscribes a handler to a specific event topic.
        /// </summary>
        /// <param name="eventName">The generic string key (e.g., "Job.Failed").</param>
        /// <param name="handler">The async method to execute when the event fires.</param>
        void Subscribe(string eventName, Func<IEvent, Task> handler);

        /// <summary>
        /// Unsubscribes a handler to prevent memory leaks.
        /// </summary>
        void Unsubscribe(string eventName, Func<IEvent, Task> handler);
    }
}