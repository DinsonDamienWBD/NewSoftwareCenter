using Microsoft.Extensions.DependencyInjection;
using SoftwareCenter.Core.Commands;
using SoftwareCenter.Core.Commands.UI;
using SoftwareCenter.Core.UI;
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
            services.AddSingleton<UiTemplateService>();
            services.AddSingleton<UiComposerService>(); // Register the UI Composer Service
            services.AddSingleton<global::SoftwareCenter.Core.UI.IUiService, UiRenderer>();

            // The Host is responsible for providing the implementation of IUIHubNotifier
            // services.AddTransient<IUIHubNotifier, ...>();

            // Register Handlers
            services.AddTransient<ICommandHandler<RegisterUIFragmentCommand, string>, RegisterUIFragmentCommandHandler>();
            services.AddTransient<ICommandHandler<UpdateUIElementCommand>, UpdateUIElementCommandHandler>();
            services.AddTransient<ICommandHandler<UnregisterUIElementCommand>, UnregisterUIElementCommandHandler>();
            services.AddTransient<ICommandHandler<RequestUITemplateCommand, string>, RequestUITemplateCommandHandler>();
            services.AddTransient<ICommandHandler<ShareUIElementOwnershipCommand>, ShareUIElementOwnershipCommandHandler>();
            services.AddTransient<ICommandHandler<ProcessUIManifestCommand>, ProcessUIManifestCommandHandler>();

            return services;
        }
    }
}
