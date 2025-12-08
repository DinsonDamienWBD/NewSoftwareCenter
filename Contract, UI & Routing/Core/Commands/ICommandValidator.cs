using System.Threading.Tasks;
using SoftwareCenter.Core.Diagnostics;

namespace SoftwareCenter.Core.Commands
{
    /// <summary>
    /// Defines a contract for validating commands before they are processed by their handlers.
    /// </summary>
    /// <typeparam name="TCommand">The type of command to validate.</typeparam>
    public interface ICommandValidator<in TCommand> where TCommand : ICommand
    {
        /// <summary>
        /// Validates the given command.
        /// </summary>
        /// <param name="command">The command instance to validate.</param>
        /// <param name="traceContext">The current trace context for the operation.</param>
        /// <returns>A Task representing the asynchronous validation operation.</returns>
        /// <exception cref="ValidationException">Thrown if the command is invalid.</exception>
        Task Validate(TCommand command, ITraceContext traceContext);
    }
}
