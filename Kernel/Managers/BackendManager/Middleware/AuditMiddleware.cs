using Core.DataWarehouse;
using Core.Log;
using Core.Pipeline;
using System.Diagnostics;

namespace Manager.Middleware
{
    /// <summary>
    /// Audit Middleware for logging message executions.
    /// </summary>
    /// <param name="dwauditLogger"></param>
    public class AuditMiddleware(IDWAuditLogger dwauditLogger)
    {
        private readonly IDWAuditLogger _dwauditLogger = dwauditLogger;

        private static readonly ActivitySource _activitySource = new("SoftwareCenter.Backend");

        /// <summary>
        /// Logs the execution of a message.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="requestorId"></param>
        /// <returns></returns>
        public async Task LogExecutionAsync(IMessage message, string requestorId)
        {
            // 1. Start OTel Activity
            using var activity = _activitySource.StartActivity($"Handle {message.GetType().Name}");

            if (activity != null)
            {
                activity.SetTag("messaging.system", "backend-bus");
                activity.SetTag("messaging.destination", message.GetType().Name);
                activity.SetTag("app.requestor_id", requestorId);
                // If the message has a TraceId (from RequestContext), we should link it here
            }

            // 2. Standard Audit Log
            await _dwauditLogger.LogAsync(
                Core.DataWarehouse.AuditAction.Update,
                "CommandBus",
                message.GetType().Name,
                requestorId,
                "Execution Started");
        }
    }
}