using System.Threading.Tasks;
using SoftwareCenter.Core.Commands;
using SoftwareCenter.Core.Diagnostics;
using SoftwareCenter.Module.AI.Agent.Commands;
using SoftwareCenter.Module.AI.Agent.Services;

namespace SoftwareCenter.Module.AI.Agent.Handlers
{
    public class NaturalLanguageCommandHandler : ICommandHandler<NaturalLanguageCommand, string>
    {
        private readonly SemanticKernelAgent _agent;

        public NaturalLanguageCommandHandler(SemanticKernelAgent agent)
        {
            _agent = agent;
        }

        public async Task<string> Handle(NaturalLanguageCommand command, ITraceContext traceContext)
        {
            // Convert NL to action using the agent, return the agent's raw response
            var result = await _agent.ExecuteAsync(command.Input);
            return result ?? string.Empty;
        }
    }
}
