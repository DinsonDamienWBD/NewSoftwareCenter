using Core;
using Core.DataWarehouse;
using Core.Log;

namespace FrontendManager.Middleware
{
    /// <summary>
    /// Exception Middleware for UI Pipeline
    /// </summary>
    /// <param name="dwauditLogger"></param>
    /// <param name="sysLogger"></param>
    public class UiExceptionMiddleware(IDWAuditLogger dwauditLogger, ISmartLogger sysLogger)
    {
        private readonly IDWAuditLogger _dwauditLogger = dwauditLogger;
        private readonly ISmartLogger _sysLogger = sysLogger;

        /// <summary>
        /// Execute with Exception Handling
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
                // 1. Log to Audit
                await _dwauditLogger.LogAsync(
                    AuditAction.AccessDenied,
                    "Pipeline.Frontend",
                    requestName,
                    requestorId,
                    $"UI CRASH: {ex.Message}"
                );

                // 2. Log to Console
                _sysLogger.LogError($"Unhandled Exception in UI Pipeline for {requestName}", ex);

                // 3. Return Safe Failure
                return Result<TResponse>.CriticalError($"UI Error: {ex.Message}");
            }
        }
    }
}