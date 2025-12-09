using SoftwareCenter.Core.Commands.UI;
using SoftwareCenter.Core.Diagnostics;
using SoftwareCenter.UIManager.Services;
using System;
using System.Threading.Tasks;
using SoftwareCenter.Core.Commands;

namespace SoftwareCenter.UIManager.Handlers
{
    /// <summary>
    /// Handles the command to create a new UI element.
    /// </summary>
    public class CreateElementCommandHandler : ICommandHandler<CreateUIElementCommand, string>
    {
        private readonly UIStateService _uiStateService;

        public CreateElementCommandHandler(UIStateService uiStateService)
        {
            _uiStateService = uiStateService;
        }

        public Task<string> Handle(CreateUIElementCommand command, ITraceContext traceContext)
        {
            if (!traceContext.Items.TryGetValue("ModuleId", out var ownerModuleIdObj) || !(ownerModuleIdObj is string ownerModuleId))
            {
                throw new InvalidOperationException("Could not determine the owner module from the trace context.");
            }

            // The UIStateService will create the element and publish the event.
            // We need to determine the priority and slot from the command's properties.
            command.InitialProperties.TryGetValue("Priority", out var priorityObj);
            command.InitialProperties.TryGetValue("SlotName", out var slotNameObj);

            var element = _uiStateService.CreateElement(
                ownerModuleId,
                (Core.UI.ElementType)Enum.Parse(typeof(Core.UI.ElementType), command.ElementType, true),
                command.ParentId,
                priorityObj is int priority ? priority : 0,
                slotNameObj as string,
                command.InitialProperties
            );

            if (element == null)
            {
                throw new InvalidOperationException("Failed to create UI element.");
            }

            return Task.FromResult(element.Id);
        }
    }
}
