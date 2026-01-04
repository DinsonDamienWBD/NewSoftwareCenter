using Core.Log;
using Core.Pipeline;
using System.Diagnostics; // OpenTelemetry Standard

namespace FrontendManager.Middleware
{
    /// <summary>
    /// Audit Middleware for UI Bus messages
    /// </summary>
    /// <param name="dwauditLogger"></param>
    public class UiAuditMiddleware(IDWAuditLogger dwauditLogger)
    {
        private readonly IDWAuditLogger _dwauditLogger = dwauditLogger;

        // Distinct Source Name so we can filter Backend vs Frontend traces
        private static readonly ActivitySource _activitySource = new("SoftwareCenter.Frontend");

        /// <summary>
        /// Log Execution of a UI Bus Message
        /// </summary>
        /// <param name="message"></param>
        /// <param name="requestorId"></param>
        /// <returns></returns>
        public async Task LogExecutionAsync(IMessage message, string requestorId)
        {
            // 1. Start OTel Activity (The "Span")
            using var activity = _activitySource.StartActivity($"UI Handle {message.GetType().Name}");

            if (activity != null)
            {
                activity.SetTag("messaging.system", "ui-bus");
                activity.SetTag("messaging.destination", message.GetType().Name);
                activity.SetTag("app.requestor_id", requestorId);
            }

            // 2. Persistent Audit Log (Optional for UI, but good for "Navigate" history)
            // We only log "Commands", not high-frequency events to avoid noise
            if (!message.GetType().Name.EndsWith("Query") && !message.GetType().Name.EndsWith("Event"))
            {
                await _dwauditLogger.LogAsync(
                    Core.DataWarehouse.Kernel.AuditAction.Update,
                    "UiBus",
                    message.GetType().Name,
                    requestorId,
                    "UI Interaction Started");
            }
        }
    }
}