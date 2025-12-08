using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using SoftwareCenter.Core.Commands;
using SoftwareCenter.Core.Diagnostics;

namespace SoftwareCenter.Kernel.Services
{
    /// <summary>
    /// A command bus that uses the .NET dependency injection container to find and execute command handlers.
    /// </summary>
    public class CommandBus : ICommandBus
    {
        private readonly ISmartCommandRouter _router;

        public CommandBus(ISmartCommandRouter router)
        {
            _router = router;
        }

        public Task Dispatch(ICommand command, ITraceContext traceContext = null)
        {
            var context = traceContext ?? new TraceContext();
            return _router.Route(command, context);
        }

        public Task<TResult> Dispatch<TResult>(ICommand<TResult> command, ITraceContext traceContext = null)
        {
            var context = traceContext ?? new TraceContext();
            return _router.Route<TResult>(command, context);
        }
    }
}
