using System;

namespace SoftwareCenter.Core.Diagnostics
{
    /// <summary>
    /// Represents a single step in the journey of a request.
    /// Immutable struct for performance.
    /// </summary>
    public struct TraceHop
    {
        public DateTime Timestamp { get; }
        public string EntityId { get; } // "Host", "Kernel", "GitModule"
        public string Action { get; }   // "Sent", "Received", "Published"

        public TraceHop(string entityId, string action)
        {
            Timestamp = DateTime.UtcNow;
            EntityId = entityId;
            Action = action;
        }
    }
}