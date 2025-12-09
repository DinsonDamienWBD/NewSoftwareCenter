using SoftwareCenter.Core.Commands.UI;
using SoftwareCenter.Core.Diagnostics;
using SoftwareCenter.UIManager.Services;
using System.Threading.Tasks;
using SoftwareCenter.Core.Commands;
using System;

namespace SoftwareCenter.UIManager.Handlers
{
    public class UnregisterUIElementCommandHandler : ICommandHandler<UnregisterUIElementCommand>
    {
        private readonly UIStateService _uiStateService;

        public UnregisterUIElementCommandHandler(UIStateService uiStateService)
        {
            _uiStateService = uiStateService;
        }

        public Task Handle(UnregisterUIElementCommand command, ITraceContext traceContext)
        {
            var element = _uiStateService.GetElement(command.ElementId);
            if (element == null)
            {
                return Task.CompletedTask; // Or handle error: element not found
            }

            if (!traceContext.Items.TryGetValue("ModuleId", out var ownerModuleIdObj) || !(ownerModuleIdObj is string ownerModuleId))
            {
                throw new InvalidOperationException("Could not determine the owner module from the trace context.");
            }

            // Basic ownership check
            if (element.OwnerModuleId != ownerModuleId)
            {
                // Or handle error: not the owner
                return Task.CompletedTask;
            }

            _uiStateService.DeleteElement(command.ElementId);

            return Task.CompletedTask;
        }
    }
}
