using Core;
using Core.Backend.Contracts;
using Core.Frontend.Contracts;
using Core.Frontend.Contracts.Models;
using Core.Modules.Contracts;

namespace Host.Modules
{
    /// <summary>
    /// System Module providing core system functionalities.
    /// </summary>
    public class SystemModule : IModule
    {
        /// <summary>
        /// Module Identifier.
        /// </summary>
        public string ModuleId => "System";

        /// <summary>
        /// Module Name.
        /// </summary>
        public string ModuleName => "System Core";

        /// <summary>
        /// Module Context.
        /// </summary>
        private ModuleContext _context = default!;

        /// <summary>
        /// Initialize Module with Context - Handshake Phase.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public Task InitializeAsync(ModuleContext context)
        {
            _context = context;
            // In the future, we load "system/settings.json" here
            return Task.CompletedTask;
        }

        /// <summary>
        /// Register Backend and Frontend Handlers.
        /// </summary>
        /// <param name="backend"></param>
        /// <param name="frontend"></param>
        public void Register(IBackendRegistry backend, IFrontendRegistry frontend)
        {
            // Register Frontend Components
            var assetsPath = Path.Combine(AppContext.BaseDirectory, "Assets");

            // A. The Shell (Main Layout)
            // 1. This is the "Skeleton" index.html
            if (File.Exists(Path.Combine(assetsPath, "index.html")))
            {
                var shellHtml = File.ReadAllText(Path.Combine(assetsPath, "index.html"));
                frontend.RegisterUi(new UiRegistrationEntry
                {
                    Id = "Shell",
                    OwnerId = ModuleId,
                    Content = shellHtml,
                    Type = "Layout"
                });
            }

            // 2. Global Styles (Existing)
            if (File.Exists(Path.Combine(assetsPath, "style.css")))
            {
                var shellcss = File.ReadAllText(Path.Combine(assetsPath, "style.css"));
                frontend.RegisterUi(new UiRegistrationEntry
                {
                    Id = "GlobalStyles",
                    OwnerId = ModuleId,
                    Type = "Style",
                    Content = shellcss,
                    Priority = UiPriority.Low
                });
            }

            // B. The Widgets (The "Organs" to inject)

            // Titlebar -> zone-titlebar
            RegisterWidgetFile(frontend, assetsPath, "titlebar.html", "SystemTitlebar", "zone-titlebar", UiPriority.Critical);

            // NavRail -> zone-nav
            // Note: Checking for both common names just in case
            var navFile = File.Exists(Path.Combine(assetsPath, "navrail.html")) ? "navrail.html" : "nav.html";
            RegisterWidgetFile(frontend, assetsPath, navFile, "SystemNav", "zone-nav", UiPriority.High);

            // Dashboard (Initial Content) -> zone-content
            RegisterWidgetFile(frontend, assetsPath, "dashboard.html", "SystemDashboard", "zone-content", UiPriority.Normal);

            // Templates -> standard-templates.html (Hidden library)
            // We register this so the Composition Engine can find it later
            if (File.Exists(Path.Combine(assetsPath, "standard-templates.html")))
            {
                var tplHtml = File.ReadAllText(Path.Combine(assetsPath, "standard-templates.html"));
                frontend.RegisterUi(new UiRegistrationEntry
                {
                    Id = "StandardTemplates",
                    OwnerId = ModuleId,
                    Content = tplHtml,
                    Type = "Script", // Or Hidden Widget
                    IsVisible = false
                });
            }

            // 7. Settings Page (NEW)
            frontend.RegisterUi(new UiRegistrationEntry
            {
                Id = "SystemSettings",
                OwnerId = ModuleId,
                Type = "Widget",
                Content = File.ReadAllText(Path.Combine(assetsPath, "settings.html")),
                ZoneId = "zone-content", // This tells the swapper where it belongs
                Priority = UiPriority.Normal,
                IsVisible = false // Hidden by default, shown when 'Navigate' command runs
            });

            // Register Backend Handlers
            // We now map the API Route directly to the Command Type.
            // The frontend must send the payload (e.g., { target: 'home' }).

            // 1. Navigation
            // Route: api/backend/dispatch with Type="system/navigate" -> NavigateCommand
            backend.RegisterHandler<NavigateCommand, SystemHandlers>("system/navigate");

            // 2. Power
            // Route: Type="system/power" -> PowerCommand
            backend.RegisterHandler<PowerCommand, SystemHandlers>("system/power");

            // 3. Get Settings (Query)
            // Route: Type="system/get-settings" -> GetSettingsQuery
            // We specify <Message, Response, Handler> for queries
            backend.RegisterHandler<GetSettingsQuery, Result<SystemSettingsModel>, SystemHandlers>("system/get-settings");

            // 4. Save Settings (Command with Result)
            // Route: Type="system/save-settings" -> SaveSettingsCommand
            backend.RegisterHandler<SaveSettingsCommand, Result<SystemSettingsModel>, SystemHandlers>("system/save-settings");
        }

        private void RegisterWidgetFile(IFrontendRegistry frontend, string basePath, string fileName, string id, string zoneId, UiPriority priority)
        {
            var path = Path.Combine(basePath, fileName);
            if (File.Exists(path))
            {
                var html = File.ReadAllText(path);
                frontend.RegisterUi(new UiRegistrationEntry
                {
                    Id = id,
                    OwnerId = ModuleId,
                    Content = html,
                    Type = "Widget",
                    ZoneId = zoneId, // <--- This tells Middleware where to inject it
                    Priority = priority
                });
            }
        }

        /// <summary>
        /// Verification Logic.
        /// </summary>
        /// <returns></returns>
        public Task<bool> VerifyAsync()
        {
            // Check if assets exist
            var assetsPath = Path.Combine(AppContext.BaseDirectory, "Assets");
            if (!File.Exists(Path.Combine(assetsPath, "index.html"))) return Task.FromResult(false);

            return Task.FromResult(true);
        }

        /// <summary>
        /// Startup Logic.
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        public Task StartAsync(CancellationToken ct)
        {
            // Start system background tasks (e.g. Health Monitor)
            return Task.CompletedTask;
        }

        /// <summary>
        /// Shutdown Logic.
        /// </summary>
        /// <returns></returns>
        public Task ShutdownAsync()
        {
            return Task.CompletedTask;
        }
    }
}