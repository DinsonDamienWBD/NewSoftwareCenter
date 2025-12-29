using Core;
using Core.Frontend.Contracts.Models;
using Core.Log;

namespace FrontendManager.Middleware
{
    /// <summary>
    /// Middleware to enforce access control rules for UI handlers.
    /// </summary>
    /// <param name="logger"></param>
    public class AccessControlMiddleware(ISmartLogger logger)
    {
        private readonly ISmartLogger _logger = logger;

        /// <summary>
        /// Checks if the requestor is allowed to execute the target handler.
        /// Enforces 'IsInternal' privacy rules.
        /// </summary>
        public Result CheckAccess(UiRegistrationEntry targetHandler, string requestorId)
        {
            // 1. System/Host always has access
            if (requestorId == "Host" || requestorId == "System")
            {
                return Result.Success();
            }

            // 2. Internal Check (Privacy)
            // If the handler is marked 'Internal', only the Owner can execute it.
            // Note: We currently don't have 'IsInternal' on UiRegistrationEntry explicitly,
            // but we might assume handlers registered privately are internal.
            // For now, we rely on the 'IsLocked' logic or future expansion.

            // As per symmetry, if we add IsInternal to UiRegistrationEntry later, 
            // this is where the check goes.

            // 3. Lock Check
            // If the UI Element is locked, maybe we shouldn't allow external commands to mutate it?
            if (targetHandler.IsLocked && targetHandler.OwnerId != requestorId)
            {
                _logger.LogWarning($"Access Denied: Module '{requestorId}' tried to control locked UI '{targetHandler.Id}' owned by '{targetHandler.OwnerId}'.");
                return Result.Failure("Access Denied: Target UI is Locked.");
            }

            return Result.Success();
        }
    }
}