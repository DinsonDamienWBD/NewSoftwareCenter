using System.Threading.Tasks;
using SoftwareCenter.Core.Commands;

namespace SoftwareCenter.Core.Routing
{
    /// <summary>
    /// Defines the bridge between the Host and the Kernel.
    /// The Host uses this to send commands into the logic layer without knowing what handles them.
    /// </summary>
    public interface IRouter
    {
        /// <summary>
        /// Routes a command asynchronously to the appropriate handler (Kernel or Module).
        /// </summary>
        /// <param name="command">The command envelope.</param>
        /// <returns>A Task containing the result envelope.</returns>
        Task<IResult> RouteAsync(ICommand command);
    }
}