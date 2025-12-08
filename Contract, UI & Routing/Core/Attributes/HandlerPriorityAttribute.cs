using System;

namespace SoftwareCenter.Core.Attributes
{
    /// <summary>
    /// Specifies the priority of a command, event, or job handler.
    /// Higher values indicate higher priority.
    /// If multiple handlers are registered for the same contract, the one with the highest priority is typically chosen.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class HandlerPriorityAttribute : Attribute
    {
        /// <summary>
        /// Gets the priority level. Higher values mean higher priority.
        /// Default priority is 0.
        /// </summary>
        public int Priority { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="HandlerPriorityAttribute"/> class.
        /// </summary>
        /// <param name="priority">The priority level. Higher values mean higher priority.</param>
        public HandlerPriorityAttribute(int priority)
        {
            Priority = priority;
        }
    }
}
