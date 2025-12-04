using System;
using System.Collections.Generic;

namespace SoftwareCenter.Core.Events
{
    /// <summary>
    /// Represents a system-wide broadcast.
    /// Unlike Commands (1-to-1), Events are 1-to-Many.
    /// </summary>
    public interface IEvent
    {
        /// <summary>
        /// The topic/name of the event (e.g., "Job.Failed", "Download.Progress", "System.LowMemory").
        /// </summary>
        string Name { get; }

        /// <summary>
        /// The event payload.
        /// Generic dictionary to allow flexible data transfer.
        /// </summary>
        Dictionary<string, object> Data { get; }

        // TRACING
        /// <summary>
        /// When the event occurred (UTC).
        /// </summary>
        DateTime Timestamp { get; }

        /// <summary>
        /// Populated automatically by TraceContext.CurrentTraceId.
        /// Connects this event to the Command that caused it.
        /// </summary>
        Guid? TraceId { get; }

        /// <summary>
        /// Populated automatically by the Kernel Proxy.
        /// The module developer does not set this.
        /// </summary>
        string SourceId { get; }
    }
}