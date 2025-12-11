using System;
using System.Threading.Tasks;
using SoftwareCenter.Core.Commands;
using SoftwareCenter.Core.Commands.UI;
using SoftwareCenter.Core.Diagnostics;
using SoftwareCenter.UIManager.Services;

namespace SoftwareCenter.UIManager.Handlers
{
    public class SetElementPropertiesCommandHandler : ICommandHandler<SetElementPropertiesCommand>
    {
        private readonly UIStateService _uiStateService;

        public SetElementPropertiesCommandHandler(UIStateService uiStateService)
        {
            _uiStateService = uiStateService;
        }

        public Task Handle(SetElementPropertiesCommand command, ITraceContext traceContext)
        {
            var element = _uiStateService.GetElement(command.ElementId);
            if (element == null)
            {
                throw new InvalidOperationException($"Element with ID {command.ElementId} not found.");
            }

            if (!traceContext.Items.TryGetValue("ModuleId", out var ownerModuleIdObj) || !(ownerModuleIdObj is string ownerModuleId))
            {
                throw new InvalidOperationException("Could not determine the owner module from the trace context.");
            }

            if (element.OwnerModuleId != ownerModuleId)
            {
                throw new InvalidOperationException($"Module '{ownerModuleId}' does not have ownership of element '{element.Id}'.");
            }

            _uiStateService.UpdateElement(command.ElementId, command.PropertiesToSet);

            return Task.CompletedTask;
        }
    }
}
