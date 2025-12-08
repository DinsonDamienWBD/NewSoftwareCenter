using SoftwareCenter.Core.Diagnostics;
using SoftwareCenter.Core.Events;
using System;
using System.Collections.Generic;

namespace SoftwareCenter.Kernel.Data
{
    /// <summary>
    /// A concrete, general-purpose implementation of the <see cref="IEvent"/> interface.
    /// </summary>
    public class SystemEvent : IEvent
    {
        public string Name { get; }

        public Dictionary<string, object> Data { get; }

        public DateTime Timestamp { get; }

        public Guid? TraceId { get; }

        public string SourceId { get; set; }

        public SystemEvent(string name, Dictionary<string, object>? data = null)
        {
            Name = name;
            Data = data ?? new Dictionary<string, object>();
            Timestamp = DateTime.UtcNow;
            TraceId = TraceContext.CurrentTraceId;
            SourceId = "Unknown";
        }
    }
}