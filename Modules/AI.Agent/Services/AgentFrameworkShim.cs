using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using SoftwareCenter.Core.Diagnostics;

namespace SoftwareCenter.Module.AI.Agent.Services
{
    /// <summary>
    /// Compatibility shim that delegates to SemanticKernelAgent when available.
    /// </summary>
    public class AgentFrameworkShim : IDisposable
    {
        private readonly SemanticKernelAgent _agent;

        public AgentFrameworkShim(IServiceProvider sp)
        {
            _agent = sp.GetService<SemanticKernelAgent>();
        }

        public async Task InitializeAsync()
        {
            // No initialization required for the lightweight agent
            await Task.CompletedTask;
        }

        public async Task HandleCommandAsync(string commandId, object payload, ITraceContext traceContext)
        {
            if (_agent == null)
            {
                Console.WriteLine($"[AgentFrameworkShim] No agent configured. Command: {commandId}");
                return;
            }

            var input = payload?.ToString() ?? string.Empty;
            var result = await _agent.ExecuteAsync(input);
            Console.WriteLine($"[AgentFrameworkShim] Agent result: {result}");
        }

        public void Dispose()
        {
            _agent?.Dispose();
        }
    }
}
