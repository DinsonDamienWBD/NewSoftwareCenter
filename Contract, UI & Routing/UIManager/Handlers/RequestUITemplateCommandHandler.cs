using SoftwareCenter.Core.Commands;
using SoftwareCenter.Core.Commands.UI;
using SoftwareCenter.Core.Diagnostics;
using SoftwareCenter.Core.UI; // For ITemplateService
using SoftwareCenter.UIManager.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SoftwareCenter.UIManager.Handlers
{
    /// <summary>
    /// Handles the command to request a UI template and create a new UI element from it.
    /// </summary>
    public class RequestUITemplateCommandHandler : ICommandHandler<RequestUITemplateCommand, string>
    {
        private readonly UIStateService _uiStateService;
        private readonly ITemplateService _templateService;

        public RequestUITemplateCommandHandler(UIStateService uiStateService, ITemplateService templateService)
        {
            _uiStateService = uiStateService;
            _templateService = templateService;
        }

        public async Task<string> Handle(RequestUITemplateCommand command, ITraceContext traceContext)
        {
            if (!traceContext.Items.TryGetValue("ModuleId", out var ownerModuleIdObj) || !(ownerModuleIdObj is string ownerModuleId))
            {
                throw new InvalidOperationException("Could not determine the owner module from the trace context.");
            }

            // Prepare parameters for the template service.
            // These will include the element's ID (to be generated) and any other properties
            // the template might need for rendering.
            var templateParameters = new Dictionary<string, object>
            {
                { "Id", Guid.NewGuid().ToString("N") } // Generate ID here for template rendering
            };

            // Merge initial properties from the command into template parameters.
            // This allows modules to pass custom data to the template.
            if (command.InitialProperties != null)
            {
                foreach (var prop in command.InitialProperties)
                {
                    templateParameters[prop.Key] = prop.Value;
                }
            }

            // Get the HTML content from the template service.
            var htmlContent = await _templateService.GetTemplateHtml(command.TemplateType, templateParameters);

            if (string.IsNullOrEmpty(htmlContent))
            {
                throw new InvalidOperationException($"Could not retrieve HTML for template type: {command.TemplateType}");
            }
            
            // Extract the generated ID from the templateParameters to use for the UIElement.
            var elementId = templateParameters["Id"].ToString();

            // The UIStateService will create the element and publish the event.
            var element = _uiStateService.CreateElement(
                ownerId: ownerModuleId,
                elementType: (Core.UI.ElementType)Enum.Parse(typeof(Core.UI.ElementType), command.TemplateType, true), // Assuming TemplateType maps to ElementType
                parentId: command.ParentId,
                htmlContent: htmlContent,
                priority: command.InitialProperties.TryGetValue("Priority", out var priorityObj) && priorityObj is int priority ? priority : 0,
                slotName: command.InitialProperties.TryGetValue("SlotName", out var slotNameObj) ? slotNameObj as string : null,
                properties: templateParameters, // Pass all template parameters as element properties
                id: elementId // Pass the generated ID explicitly
            );

            if (element == null)
            {
                throw new InvalidOperationException("Failed to create UI element from template.");
            }

            return element.Id;
        }
    }
}
