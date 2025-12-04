using System;
using System.Collections.Generic;
using SoftwareCenter.Core.Diagnostics;

namespace SoftwareCenter.Core.Logging
{
    public class LogEntry
    {
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public LogLevel Level { get; set; }
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// The component issuing the log (e.g., "GitModule").
        /// </summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>
        /// High-level context (e.g. "Command.Execution", "System.Boot").
        /// </summary>
        public string Category { get; set; } = "General";

        // --- TRACEABILITY ---

        /// <summary>
        /// The trace ID of the transaction active when this log was created.
        /// </summary>
        public Guid? TraceId { get; set; }

        /// <summary>
        /// Extra data for structured logging.
        /// Traceability details (like the full hop history) can be stored here 
        /// to be revealed only in Verbose mode.
        /// </summary>
        public Dictionary<string, object> ExtendedData { get; set; } = new Dictionary<string, object>();
    }
}