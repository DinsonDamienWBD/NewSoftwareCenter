using SoftwareCenter.Core.Commands;
using SoftwareCenter.Core.Commands.UI;
using SoftwareCenter.Core.Diagnostics;
using SoftwareCenter.UIManager.Services;
using System;
using System.Threading.Tasks;

namespace SoftwareCenter.UIManager.Handlers
{
    /// <summary>
    /// Handles the command to register a new UI fragment by directly providing its HTML, CSS, and JS content.
    /// </summary>
    public class RegisterUIFragmentCommandHandler : ICommandHandler<RegisterUIFragmentCommand, string>
    {
        private readonly UIStateService _uiStateService;

        public RegisterUIFragmentCommandHandler(UIStateService uiStateService)
        {
            _uiStateService = uiStateService;
        }

        public Task<string> Handle(RegisterUIFragmentCommand command, ITraceContext traceContext)
        {
            if (!traceContext.Items.TryGetValue("ModuleId", out var ownerModuleIdObj) || !(ownerModuleIdObj is string ownerModuleId))
            {
                throw new InvalidOperationException("Could not determine the owner module from the trace context.");
            }

            // The UIStateService will create the element and publish the event.
            var element = _uiStateService.CreateElement(
                ownerModuleId,
                Core.UI.ElementType.Fragment, // Assuming "Fragment" is a valid ElementType
                command.ParentId,
                command.HtmlContent,
                command.CssContent,
                command.JsContent,
                command.Priority,
                command.SlotName
            );

            if (element == null)
            {
                throw new InvalidOperationException("Failed to create UI fragment.");
            }

            return Task.FromResult(element.Id);
        }
    }
}
