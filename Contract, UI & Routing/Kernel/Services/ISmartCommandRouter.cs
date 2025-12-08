using SoftwareCenter.Core.Commands;
using SoftwareCenter.Core.Diagnostics;
using System.Threading.Tasks;

namespace SoftwareCenter.Kernel.Services
{
    /// <summary>
    /// Defines the contract for a smart command router that resolves and executes
    /// the highest priority command handler, with support for fallback.
    /// </summary>
    public interface ISmartCommandRouter
    {
        /// <summary>
        /// Resolves and executes the highest priority handler for a command that does not return a value.
        /// </summary>
        /// <param name="command">The command to handle.</param>
        /// <param name="traceContext">The context for tracing the operation.</param>
        /// <returns>A task that represents the completion of the handling process.</returns>
        Task Route(ICommand command, ITraceContext traceContext);

        /// <summary>
        /// Resolves and executes the highest priority handler for a command that returns a value.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="command">The command to handle.</param>
        /// <param name="traceContext">The context for tracing the operation.</param>
        /// <returns>The result from the command handler.</returns>
        Task<TResult> Route<TResult>(ICommand<TResult> command, ITraceContext traceContext);
    }
}
