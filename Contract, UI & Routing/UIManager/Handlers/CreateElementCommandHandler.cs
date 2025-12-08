using System;
using System.Threading.Tasks;
using SoftwareCenter.Core.Commands;
using SoftwareCenter.Core.Commands.UI;
using SoftwareCenter.Core.Diagnostics;
using SoftwareCenter.Core.UI;
using SoftwareCenter.UIManager.Services;

namespace SoftwareCenter.UIManager.Handlers
{
    public class CreateElementCommandHandler : ICommandHandler<CreateElementCommand, Guid>
    {
        private readonly UIStateService _uiStateService;

        public CreateElementCommandHandler(UIStateService uiStateService)
        {
            _uiStateService = uiStateService;
        }

        public Task<Guid> Handle(CreateElementCommand command, ITraceContext traceContext)
        {
            // The owner module should be passed in the trace context.
            if (!traceContext.Items.TryGetValue("ModuleId", out var ownerModuleIdObj) || !(ownerModuleIdObj is string ownerModuleId))
            {
                throw new InvalidOperationException("Could not determine the owner module from the trace context.");
            }

            var newElementId = Guid.NewGuid();
            var element = new UIElement(newElementId, command.ElementType, ownerModuleId)
            {
                ParentId = command.ParentId
            };

            foreach (var prop in command.InitialProperties)
            {
                element.Properties[prop.Key] = prop.Value;
            }

            if (!_uiStateService.TryAddElement(element))
            {
                throw new InvalidOperationException($"An element with ID {newElementId} already exists.");
            }

            return Task.FromResult(newElementId);
        }
    }
}
