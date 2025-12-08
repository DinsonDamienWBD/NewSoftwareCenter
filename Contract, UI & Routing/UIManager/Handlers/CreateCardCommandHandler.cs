using System.Threading;
using System.Threading.Tasks;
using SoftwareCenter.Core.Commands;
using SoftwareCenter.Kernel.Data;
using SoftwareCenter.UIManager.Commands;
using SoftwareCenter.UIManager.Services;

namespace SoftwareCenter.UIManager.Handlers
{
    public class CreateCardCommandHandler : ICommandHandler<CreateCardCommand>
    {
        private readonly UIStateService _uiStateService;

        public CreateCardCommandHandler(UIStateService uiStateService)
        {
            _uiStateService = uiStateService;
        }

        public Task<IResult> Handle(CreateCardCommand command, CancellationToken cancellationToken)
        {
            _uiStateService.CreateCard(command.ContainerId, command.Title);
            return Task.FromResult<IResult>(Result.FromSuccess());
        }
    }
}