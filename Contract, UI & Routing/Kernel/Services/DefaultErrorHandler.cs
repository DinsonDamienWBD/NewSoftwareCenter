using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SoftwareCenter.Core.Commands;
using SoftwareCenter.Core.Errors;
using SoftwareCenter.Core.Diagnostics;
using SoftwareCenter.Core.Logs;

using Microsoft.Extensions.Logging;

namespace SoftwareCenter.Kernel.Services
{
    /// <summary>
    /// Default implementation of <see cref="IErrorHandler"/> that logs errors
    /// by dispatching a <see cref="LogCommand"/> through the command bus.
    /// </summary>
    public class DefaultErrorHandler : IErrorHandler
    {
        private readonly ILogger<DefaultErrorHandler> _logger;

        public DefaultErrorHandler(ILogger<DefaultErrorHandler> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task HandleError(Exception exception, ITraceContext traceContext, string message = null, bool isCritical = false)
        {
            var logMessage = message ?? "An unhandled error occurred.";

            // Use the injected logger directly to avoid circular dependencies with the command bus.
            // ILogger's methods (LogCritical, LogError) accept an Exception object directly.
            if (isCritical)
            {
                _logger.LogCritical(exception, "CRITICAL: {LogMessage} (TraceId: {TraceId})", logMessage, traceContext?.TraceId);
            }
            else
            {
                _logger.LogError(exception, "ERROR: {LogMessage} (TraceId: {TraceId})", logMessage, traceContext?.TraceId);
            }
            await Task.CompletedTask; // Since ILogger is synchronous, return a completed task
        }
    }
}
