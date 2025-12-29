using Core.Frontend.Contracts.Models;
using Core.Log;

namespace FrontendManager.Middleware
{
    /// <summary>
    /// Detects and logs usage of deprecated UI elements.
    /// </summary>
    /// <param name="logger"></param>
    public class UiDeprecationMiddleware(ISmartLogger logger)
    {
        private readonly ISmartLogger _logger = logger;

        /// <summary>
        /// Checks if the given UI registration entry is deprecated and logs a warning if so.
        /// </summary>
        /// <param name="entry"></param>
        /// <param name="requestorId"></param>
        public void CheckDeprecation(UiRegistrationEntry entry, string requestorId)
        {
            if (!string.IsNullOrEmpty(entry.DeprecationMessage))
            {
                _logger.LogWarning(
                    $"[UI DEPRECATION] Module '{requestorId}' is rendering a deprecated UI element.",
                    properties: new Dictionary<string, object>
                    {
                        { "ElementId", entry.Id },
                        { "ElementOwner", entry.OwnerId },
                        { "Reason", entry.DeprecationMessage }
                    }
                );
            }
        }
    }
}