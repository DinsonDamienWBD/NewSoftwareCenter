using SoftwareCenter.Core.Commands;
using SoftwareCenter.Core.Commands.UI;
using SoftwareCenter.Core.Diagnostics;
using SoftwareCenter.Core.Errors;
using System.Threading.Tasks;

namespace SoftwareCenter.UIManager.Validation
{
    /// <summary>
    /// Validates the CreateElementCommand to ensure it has valid parameters.
    /// </summary>
    public class CreateElementCommandValidator : ICommandValidator<CreateElementCommand>
    {
        public Task Validate(CreateElementCommand command, ITraceContext traceContext)
        {
            if (!Enum.IsDefined(typeof(SoftwareCenter.Core.UI.ElementType), command.ElementType))
            {
                throw new ValidationException($"Invalid ElementType '{command.ElementType}'.");
            }

            // Example: If a specific element type always requires a parent
            // if (command.ElementType == SoftwareCenter.Core.UI.ElementType.Button && !command.ParentId.HasValue)
            // {
            //     throw new ValidationException("Buttons must have a parent element.");
            // }

            return Task.CompletedTask;
        }
    }
}
