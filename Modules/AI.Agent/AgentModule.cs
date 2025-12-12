using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SoftwareCenter.Core.Modules;
using System.Threading.Tasks;

namespace SoftwareCenter.Module.AI.Agent
{
    public class AgentModule : IModule
    {
        public string Id => "SoftwareCenter.AIAgent";
        public string Name => "AI Agent";

        public void ConfigureServices(IServiceCollection services)
        {
            // Register the lightweight SemanticKernelAgent
            services.AddTransient<Services.SemanticKernelAgent>();

            // Register the shim that modules can consume
            services.AddSingleton<Services.AgentFrameworkShim>();

            // Register this module so ModuleLoader can find and initialize it
            services.AddSingleton<IModule, AgentModule>();

            // Register NL command and its handler so ModuleLoader will discover it
            services.AddTransient<Commands.NaturalLanguageCommand>();
            services.AddTransient<Handlers.NaturalLanguageCommandHandler>();
            services.AddTransient<SoftwareCenter.Core.Commands.ICommandHandler<Commands.NaturalLanguageCommand, string>, Handlers.NaturalLanguageCommandHandler>();
        }

        public Task Initialize(System.IServiceProvider serviceProvider)
        {
            // No-op: handler registration is handled by DI and ModuleLoader discovery
            return Task.CompletedTask;
        }
    }
}
