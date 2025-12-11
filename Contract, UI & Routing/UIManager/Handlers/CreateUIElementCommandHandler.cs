using SoftwareCenter.Core.Commands.UI;
using SoftwareCenter.Core.Diagnostics;
using SoftwareCenter.UIManager.Services;
using System;
using System.Threading.Tasks;
using SoftwareCenter.Core.Commands;
using SoftwareCenter.Core.Errors;

namespace SoftwareCenter.UIManager.Handlers
{
    /// <summary>
    /// Handles the command to create a new UI element.
    /// </summary>
    public class CreateUIElementCommandHandler : ICommandHandler<CreateUIElementCommand, string>
    {
        private readonly UIStateService _uiStateService;

        /// <summary>
        /// Initializes a new instance of the <see cref="CreateUIElementCommandHandler"/> class.
        /// </summary>
        /// <param name="uiStateService">The UI state service.</param>
        public CreateUIElementCommandHandler(UIStateService uiStateService)
        {
            _uiStateService = uiStateService;
        }

        /// <summary>
        /// Handles the <see cref="CreateUIElementCommand"/>.
        /// </summary>
        /// <param name="command">The command to handle.</param>
        /// <param name="traceContext">The trace context.</param>
        /// <returns>The ID of the newly created element.</returns>
        /// <exception cref="ValidationException">Thrown if the element type in the command is invalid.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the element fails to be created.</exception>
        public Task<string> Handle(CreateUIElementCommand command, ITraceContext traceContext)
        {
            if (!Enum.TryParse(command.ElementType, true, out Core.UI.ElementType elementType))
            {
                throw new ValidationException($"Invalid ElementType '{command.ElementType}'.");
            }

            command.InitialProperties.TryGetValue("Priority", out var priorityObj);
            command.InitialProperties.TryGetValue("SlotName", out var slotNameObj);
            command.InitialProperties.TryGetValue("Id", out var idObj);

            var element = _uiStateService.CreateElement(
                ownerId: command.OwnerModuleId,
                elementType: elementType,
                parentId: command.ParentId,
                htmlContent: "",
                priority: priorityObj is int priority ? priority : 0,
                slotName: slotNameObj as string,
                properties: command.InitialProperties,
                id: idObj as string
            );

            if (element == null)
            {
                throw new InvalidOperationException("Failed to create UI element.");
            }

            return Task.FromResult(element.Id);
        }
    }
}
