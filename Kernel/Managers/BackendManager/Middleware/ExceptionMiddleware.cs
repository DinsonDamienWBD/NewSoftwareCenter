using Core;
using Core.DataWarehouse;
using Core.Log;
using Microsoft.Extensions.Logging; // System Logger fallback

namespace BackendManager.Middleware
{
    /// <summary>
    /// Exception Middleware
    /// </summary>
    /// <param name="auditLogger"></param>
    /// <param name="dwauditLogger"></param>
    /// <param name="sysLogger"></param>
    public class ExceptionMiddleware(IAuditLogger auditLogger, IDWAuditLogger dwauditLogger, ISmartLogger sysLogger)
    {
        private readonly Core.Log.IAuditLogger _auditLogger = auditLogger;
        private readonly Core.Log.IDWAuditLogger _dwauditLogger = dwauditLogger;
        private readonly Core.Log.ISmartLogger _sysLogger = sysLogger;

        /// <summary>
        /// Executes the next delegate in a try-catch block to handle unhandled exceptions.
        /// </summary>
        /// <typeparam name="TResponse"></typeparam>
        /// <param name="next"></param>
        /// <param name="requestName"></param>
        /// <param name="requestorId"></param>
        /// <returns></returns>
        public async Task<Result<TResponse>> ExecuteAsync<TResponse>(
            Func<Task<Result<TResponse>>> next,
            string requestName,
            string requestorId)
        {
            try
            {
                return await next();
            }
            catch (Exception ex)
            {
                // 1. Log to Audit (The "Black Box")
                await _dwauditLogger.LogAsync(
                    AuditAction.AccessDenied, // Using 'AccessDenied' or similar failure action
                    "Pipeline.Backend",
                    requestName,
                    requestorId,
                    $"CRASH: {ex.Message}"
                );

                // 2. Log to Developer Console
                _sysLogger.LogError($"Unhandled Exception in Backend Pipeline for {requestName}", ex);

                // 3. Return Safe Failure
                return Result<TResponse>.CriticalError($"Internal System Error: {ex.Message}");
            }
        }
    }
}