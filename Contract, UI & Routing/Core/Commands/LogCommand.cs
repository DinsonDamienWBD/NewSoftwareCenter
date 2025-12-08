using System;
using System.Collections.Generic;
using SoftwareCenter.Core.Commands;
using SoftwareCenter.Core.Diagnostics;
using SoftwareCenter.Core.Logs;

namespace SoftwareCenter.Core.Commands
{
    /// <summary>
    /// A command to log a message at a specific level.
    /// This command will be handled by the highest priority ILogCommandHandler.
    /// </summary>
    public class LogCommand : ICommand
    {
        /// <summary>
        /// Gets the severity level of the log message.
        /// </summary>
        public LogLevel Level { get; }

        /// <summary>
        /// Gets the log message content.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets optional exception details associated with the log message.
        /// </summary>
        public string ExceptionDetails { get; }

        /// <summary>
        /// Gets the TraceId from the operation that generated this log.
        /// </summary>
        public Guid TraceId { get; }

        /// <summary>
        /// Gets the ModuleId that initiated this log request.
        /// </summary>
        public string InitiatingModuleId { get; }

        /// <summary>
        /// Gets additional structured data for the log entry.
        /// </summary>
        public Dictionary<string, object> StructuredData { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="LogCommand"/> class.
        /// </summary>
        /// <param name="level">The severity level of the log message.</param>
        /// <param name="message">The log message content.</param>
        /// <param name="traceContext">The trace context from the operation generating the log.</param>
        /// <param name="exception">Optional exception details.</param>
        /// <param name="structuredData">Optional additional structured data.</param>
        public LogCommand(LogLevel level, string message, ITraceContext traceContext, Exception exception = null, Dictionary<string, object> structuredData = null)
        {
            Level = level;
            Message = message;
            TraceId = traceContext?.TraceId ?? Guid.Empty;
            InitiatingModuleId = traceContext?.Items.TryGetValue("ModuleId", out var id) == true ? id as string : "Unknown";
            ExceptionDetails = exception?.ToString();
            StructuredData = structuredData ?? new Dictionary<string, object>();
        }
    }
}
