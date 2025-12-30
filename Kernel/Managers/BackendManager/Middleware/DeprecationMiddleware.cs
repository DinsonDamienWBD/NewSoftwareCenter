using Core.Backend.Contracts.Models;
using Core.Log;

namespace Manager.Middleware
{
    /// <summary>
    /// Deprecation Middleware
    /// </summary>
    /// <param name="logger"></param>
    public class DeprecationMiddleware(ISmartLogger logger)
    {
        private readonly ISmartLogger _logger = logger;

        /// <summary>
        /// Checks if the target handler is marked as Obsolete.
        /// If so, logs a warning but DOES NOT stop execution (Graceful Degradation).
        /// </summary>
        public void CheckDeprecation(ServiceRegistrationEntry handlerDef, string requestorId)
        {
            if (!string.IsNullOrEmpty(handlerDef.DeprecationMessage))
            {
                // We log this as a Warning to the Developer Console
                _logger.LogWarning(
                    $"[DEPRECATION NOTICE] Module '{requestorId}' is using a deprecated service.",
                    properties: new Dictionary<string, object>
                    {
                        { "TargetService", handlerDef.InterfaceType.Name },
                        { "TargetOwner", handlerDef.OwnerId },
                        { "Reason", handlerDef.DeprecationMessage }
                    }
                );
            }
        }
    }
}