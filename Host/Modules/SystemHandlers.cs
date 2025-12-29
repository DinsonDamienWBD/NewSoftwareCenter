using Core;
using Core.Attributes;
using Core.Backend.Contracts;
using Core.Backend.Messages;
using Core.Log;
using Core.Pipeline;
using Core.ServiceRegistry;
using Core.Services;
using System.Text.Json;

namespace Host.Modules
{
    // --- MODELS ---

    /// <summary>
    /// Settings model for the testing the system.
    /// </summary>
    public class SystemSettingsModel
    {
        /// <summary>
        /// Theme of the application.
        /// </summary>
        public string Theme { get; set; } = "dark";

        /// <summary>
        /// Language preference.
        /// </summary>
        public string Language { get; set; } = "en-US";

        /// <summary>
        /// Auto-save enabled.
        /// </summary>
        public bool AutoSave { get; set; } = true;

        /// <summary>
        /// Notification settings.
        /// </summary>
        public NotificationSettings Notifications { get; set; } = new();

        /// <summary>
        /// Notification settings model.
        /// </summary>
        public class NotificationSettings
        {
            /// <summary>
            /// Email notifications enabled.
            /// </summary>
            public bool Email { get; set; }

            /// <summary>
            /// Desktop notifications enabled.
            /// </summary>
            public bool Desktop { get; set; } = true;
        }
    }

    /// <summary>
    /// Navigation command to change views.
    /// </summary>
    public class NavigateCommand : MessageBase
    {
        /// <summary>
        /// Target view to navigate to.
        /// Make sure this is public with getter/setter.
        /// </summary>
        public string Target { get; set; } = string.Empty;
    }

    /// <summary>
    /// Power command to perform system actions.
    /// </summary>
    public class PowerCommand : MessageBase
    {
        /// <summary>
        /// Action to perform: "shutdown", "restart-web", "reboot-os".
        /// </summary>
        public string Action { get; set; } = string.Empty;
    }

    /// <summary>
    /// Query to get current system settings.
    /// </summary>
    public class GetSettingsQuery : MessageBase { }

    /// <summary>
    /// Save settings command to update system settings.
    /// </summary>
    public class SaveSettingsCommand : MessageBase
    {
        /// <summary>
        /// Theme of the application.
        /// </summary>
        public string Theme { get; set; } = "dark";

        /// <summary>
        /// Language preference.
        /// </summary>
        public string Language { get; set; } = "en-US";

        /// <summary>
        /// Auto-save enabled.
        /// </summary>
        public bool AutoSave { get; set; }

        /// <summary>
        /// Notification settings.
        /// </summary>
        public SystemSettingsModel.NotificationSettings Notifications { get; set; } = new();
    }

    /// <summary>
    /// Settings changed event to notify subscribers.
    /// </summary>
    public class SettingsChangedEvent : MessageBase, IEvent
    {
        /// <summary>
        /// System settings after the change.
        /// </summary>
        public SystemSettingsModel NewSettings { get; set; } = new();
    }

    /// <summary>
    /// System Handlers for navigation, power actions, and settings management.
    /// </summary>
    [Uses(typeof(SettingsChangedEvent))]
    public class SystemHandlers :
        IHandler<NavigateCommand>,
        IHandler<PowerCommand>,
        IHandler<GetSettingsQuery, Result<SystemSettingsModel>>,
        IHandler<SaveSettingsCommand, Result<SystemSettingsModel>>
    {
        private readonly ISmartLogger _logger;
        private readonly IHostApplicationLifetime _lifetime;
        private readonly IBackendPipeline _bus;
        private readonly IFileSystemProvider _fileSystem; // NEW
        private readonly string _settingsPath;
        private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

        /// <summary>
        /// System Handlers Constructor.
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="lifetime"></param>
        /// <param name="bus"></param>
        /// <param name="fileSystem"></param>
        public SystemHandlers(
            ISmartLogger logger,
            IHostApplicationLifetime lifetime,
            IBackendPipeline bus,
            IFileSystemProvider fileSystem)
        {
            _logger = logger;
            _lifetime = lifetime;
            _bus = bus;
            _fileSystem = fileSystem;

            var dataDir = Path.Combine(AppContext.BaseDirectory, "Data");
            // Note: We use synchronous IO here in constructor for simplicity, 
            // but ideally this moves to InitializeAsync or we rely on the provider ensuring paths.
            if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);
            _settingsPath = Path.Combine(dataDir, "system.json");
        }

        /// <summary>
        /// Navigation Command Handler.
        /// </summary>
        /// <param name="command"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public Task HandleAsync(NavigateCommand command, CancellationToken ct)
        {
            // Log this to prove we reached the handler
            _logger.LogInfo($"[SystemHandler] Navigation Success! Target: {command.Target}");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Power Command Handler.
        /// </summary>
        /// <param name="command"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public Task HandleAsync(PowerCommand command, CancellationToken ct)
        {
            _logger.LogWarning($"[SystemHandler] Power Action: {command.Action}");

            if (command.Action == "shutdown")
            {
                Task.Run(async () =>
                {
                    await Task.Delay(1000); // Wait for HTTP response to go out
                    _logger.LogWarning("Terminating Process...");
                    _lifetime.StopApplication();
                    Environment.Exit(0); // FORCE KILL
                }, CancellationToken.None);
            }
            else if (command.Action == "restart-web")
            {
                Task.Run(async () =>
                {
                    await Task.Delay(1000);
                    var fileName = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                    if (!string.IsNullOrEmpty(fileName))
                    {
                        // Spawn new process
                        System.Diagnostics.Process.Start(fileName);
                    }
                    _lifetime.StopApplication();
                    Environment.Exit(0); // FORCE KILL
                }, CancellationToken.None);
            }
            else if (command.Action == "reboot-os")
            {
                // Reboot OS
                Task.Run(async () =>
                {
                    await Task.Delay(1000);
                    // Execute Windows Shutdown command
                    System.Diagnostics.Process.Start("shutdown", "/r /t 0");
                    _lifetime.StopApplication();
                }, CancellationToken.None);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Settings Query Handler.
        /// </summary>
        /// <param name="query"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public async Task<Result<SystemSettingsModel>> HandleAsync(GetSettingsQuery query, CancellationToken ct)
        {
            if (!await _fileSystem.FileExistsAsync(_settingsPath, ct))
                return Result<SystemSettingsModel>.Success(new SystemSettingsModel());

            try
            {
                var json = await _fileSystem.ReadTextAsync(_settingsPath, ct);
                return Result<SystemSettingsModel>.Success(JsonSerializer.Deserialize<SystemSettingsModel>(json, _jsonOptions) ?? new SystemSettingsModel());
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to read settings", ex);
                return Result<SystemSettingsModel>.Failure("Could not load settings.");
            }
        }

        /// <summary>
        /// Save Settings Command Handler.
        /// </summary>
        /// <param name="command"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public async Task<Result<SystemSettingsModel>> HandleAsync(SaveSettingsCommand command, CancellationToken ct)
        {
            try
            {
                var model = new SystemSettingsModel
                {
                    Theme = command.Theme,
                    Language = command.Language,
                    AutoSave = command.AutoSave,
                    Notifications = command.Notifications
                };

                var json = JsonSerializer.Serialize(model, _jsonOptions);

                await _fileSystem.WriteTextAsync(_settingsPath, json, ct);

                _logger.LogInfo("System settings saved to disk.");

                // 4. Actually USE the dependency
                // This triggers the "Refers To" link in the Developer UI
                var sysContext = new RequestContext("System");
                await _bus.PublishAsync(new SettingsChangedEvent { NewSettings = model }, sysContext, ct);

                return Result<SystemSettingsModel>.Success(model);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to save settings", ex);
                return Result<SystemSettingsModel>.Failure("Could not save settings.");
            }
        }
    }
}