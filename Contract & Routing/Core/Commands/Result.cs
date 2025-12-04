using System;
using System.Collections.Generic;
using SoftwareCenter.Core.Diagnostics; // For TraceId and TraceHop

namespace SoftwareCenter.Core.Commands
{
    /// <summary>
    /// Concrete implementation of IResult.
    /// Provides static factory methods for convenient creation of success/failure responses.
    /// Automatically binds to the current TraceContext.
    /// </summary>
    public class Result : IResult
    {
        public bool Success { get; protected set; }
        public string Message { get; protected set; }
        public object? Data { get; protected set; }

        // --- TRACING ---
        public Guid TraceId { get; private set; }
        public List<TraceHop> History { get; private set; }

        /// <summary>
        /// Protected constructor to force use of static factory methods.
        /// Automatically captures the ambient TraceId.
        /// </summary>
        protected Result(bool success, string message, object? data)
        {
            Success = success;
            Message = message;
            Data = data;

            // Auto-Link: Grab the invisible TraceId from the async context.
            // If we are in a valid request chain, this will link the Result to the Command.
            TraceId = TraceContext.CurrentTraceId ?? Guid.Empty;

            History = new List<TraceHop>();
        }

        // --- FACTORY METHODS ---

        /// <summary>
        /// Creates a successful result with no data.
        /// </summary>
        public static Result FromSuccess()
        {
            return new Result(true, "Operation successful.", null);
        }

        /// <summary>
        /// Creates a successful result with a payload.
        /// </summary>
        /// <param name="data">The return value (e.g., a List of Files).</param>
        /// <param name="message">Optional success message.</param>
        public static Result FromSuccess(object? data = null, string message = "Operation successful.")
        {
            return new Result(true, message, data);
        }

        /// <summary>
        /// Creates a failure result.
        /// </summary>
        /// <param name="message">Error description.</param>
        public static Result FromFailure(string message)
        {
            return new Result(false, message, null);
        }

        /// <summary>
        /// Creates a failure result from an Exception.
        /// <param name="ex"/> The exception that caused the failure.</param>
        /// </summary>
        public static Result FromFailure(Exception ex)
        {
            return new Result(false, ex.Message, ex);
        }
    }
}