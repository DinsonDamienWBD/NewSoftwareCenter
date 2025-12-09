using SoftwareCenter.Core.Commands.UI;
using SoftwareCenter.Core.Diagnostics;
using SoftwareCenter.UIManager.Services;
using System.Threading.Tasks;
using SoftwareCenter.Core.Commands;
using System;
using System.Collections.Generic;

namespace SoftwareCenter.UIManager.Handlers
{
    /// <summary>
    /// Handles the command to update an existing UI element's properties.
    /// </summary>
    public class UpdateUIElementCommandHandler : ICommandHandler<UpdateUIElementCommand>
    {
        private readonly UIStateService _uiStateService;

        public UpdateUIElementCommandHandler(UIStateService uiStateService)
        {
            _uiStateService = uiStateService;
        }

        public Task Handle(UpdateUIElementCommand command, ITraceContext traceContext)
        {
            var element = _uiStateService.GetElement(command.ElementId);
            if (element == null)
            {
                // Element not found, or not owned by this module
                return Task.CompletedTask;
            }

            if (!traceContext.Items.TryGetValue("ModuleId", out var ownerModuleIdObj) || !(ownerModuleIdObj is string ownerModuleId))
            {
                throw new InvalidOperationException("Could not determine the owner module from the trace context.");
            }

            // Basic ownership check
            if (element.OwnerModuleId != ownerModuleId)
            {
                // Not the owner, or no sufficient permissions
                return Task.CompletedTask;
            }

            _uiStateService.UpdateElement(command.ElementId, command.UpdatedProperties);

            return Task.CompletedTask;
        }
    }
}
