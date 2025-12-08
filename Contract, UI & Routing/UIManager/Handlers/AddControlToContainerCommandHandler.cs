using System.Threading;
using System.Threading.Tasks;
using SoftwareCenter.Core.Commands;
using SoftwareCenter.Kernel.Data;
using SoftwareCenter.UIManager.Commands;
using SoftwareCenter.UIManager.Services;

namespace SoftwareCenter.UIManager.Handlers
{
    public class AddControlToContainerCommandHandler : ICommandHandler<AddControlToContainerCommand>
    {
        private readonly UIStateService _uiStateService;

        public AddControlToContainerCommandHandler(UIStateService uiStateService)
        {
            _uiStateService = uiStateService;
        }

        public Task<IResult> Handle(AddControlToContainerCommand command, CancellationToken cancellationToken)
        {
            _uiStateService.AddControlToContainer(command.ContainerId, command.ControlType, command.ControlId);
            return Task.FromResult<IResult>(Result.FromSuccess());
        }
    }
}