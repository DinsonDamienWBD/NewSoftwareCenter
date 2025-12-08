using Microsoft.Extensions.DependencyInjection;
using SoftwareCenter.Core.Commands;
using SoftwareCenter.Core.Commands.UI;
using SoftwareCenter.UIManager.Handlers;
using SoftwareCenter.UIManager.Services;

namespace SoftwareCenter.UIManager
{
    public static class UIManagerServiceCollectionExtensions
    {
        public static IServiceCollection AddUIManager(this IServiceCollection services)
        {
            // Register UIManager services
            services.AddSingleton<UIStateService>();

            // Register Handlers
            services.AddTransient<ICommandHandler<CreateElementCommand, System.Guid>, CreateElementCommandHandler>();
            services.AddTransient<ICommandHandler<SetElementPropertiesCommand>, SetElementPropertiesCommandHandler>();

            return services;
        }
    }
}
