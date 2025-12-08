using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SoftwareCenter.Kernel.Services;

namespace SoftwareCenter.Kernel
{
    public static class KernelServiceCollectionExtensions
    {
        public static IServiceCollection AddKernel(this IServiceCollection services)
        {
            services.AddSingleton<ServiceRegistry>();
            services.AddSingleton<IEventBus, EventBus>();
            services.AddSingleton<CommandBus>();
            services.AddSingleton<ModuleLoader>();
            services.AddSingleton<StandardKernel>();

            return services;
        }
    }
}