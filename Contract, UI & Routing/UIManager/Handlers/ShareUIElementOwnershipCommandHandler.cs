using SoftwareCenter.Core.Commands.UI;
using SoftwareCenter.Core.Diagnostics;
using SoftwareCenter.UIManager.Services;
using System.Threading.Tasks;
using SoftwareCenter.Core.Commands;
using SoftwareCenter.Core.UI;
using System;
using System.Linq;

namespace SoftwareCenter.UIManager.Handlers
{
    /// <summary>
    /// Handles the command to share ownership or grant permissions for a UI element to another module.
    /// </summary>
    public class ShareUIElementOwnershipCommandHandler : ICommandHandler<ShareUIElementOwnershipCommand>
    {
        private readonly UIStateService _uiStateService;

        public ShareUIElementOwnershipCommandHandler(UIStateService uiStateService)
        {
            _uiStateService = uiStateService;
        }

        public Task Handle(ShareUIElementOwnershipCommand command, ITraceContext traceContext)
        {
            var element = _uiStateService.GetElement(command.ElementId);
            if (element == null)
            {
                // Element not found
                return Task.CompletedTask;
            }

            if (!traceContext.Items.TryGetValue("ModuleId", out var ownerModuleIdObj) || !(ownerModuleIdObj is string ownerModuleId))
            {
                throw new InvalidOperationException("Could not determine the owner module from the trace context.");
            }

            // Only the current owner can share or modify ownership.
            if (element.AccessControl.OwnerId != ownerModuleId)
            {
                // Not the owner, or no sufficient permissions
                return Task.CompletedTask;
            }

            // Create a copy of the existing access control and update it.
            var newAccessControl = new UIAccessControl
            {
                OwnerId = element.AccessControl.OwnerId,
                SharedAccess = element.AccessControl.SharedAccess.ToDictionary(entry => entry.Key, entry => entry.Value) // Deep copy
            };

            // Add or update the target module's permissions.
            newAccessControl.SharedAccess[command.TargetModuleId] = command.Permissions;

            _uiStateService.SetAccessControl(command.ElementId, newAccessControl);

            return Task.CompletedTask;
        }
    }
}
