using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using SoftwareCenter.Kernel;
using SoftwareCenter.UIManager;
using SoftwareCenter.Host.Hubs;
using SoftwareCenter.Host.Services;
using System;
using System.IO;
using System.Reflection;
using SoftwareCenter.Core.Commands;
using System.Text.Json;
using SoftwareCenter.Core.Diagnostics;
using SoftwareCenter.Core.Discovery.Commands;
using SoftwareCenter.Kernel.Services;
using System.Diagnostics;
using System.Linq;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

#region Service Registration

// 1. Add Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole().SetMinimumLevel(LogLevel.Debug);
// Note: FileLoggerProvider is in Kernel, will be registered via AddKernel if needed.

// 2. Add Core Infrastructure (Kernel)
// The Kernel is responsible for core services, command bus, and loading modules.
builder.Services.AddKernel();

// 3. Add UI Manager
// The UIManager handles UI state and composition.
builder.Services.AddUIManager();

// 4. Add Host-specific Services
builder.Services.AddControllers();
builder.Services.AddSignalR();

// Registers the service that populates the UI with the Host's own features on startup.
builder.Services.AddHostedService<HostFeatureUIService>();

// Register the Host's notifier as the implementation for the UIManager's interface
builder.Services.AddTransient<SoftwareCenter.UIManager.Services.IUIHubNotifier, UIHubNotifier>();

#endregion

var app = builder.Build();

#region Middleware Pipeline

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles(); // Serves files from wwwroot

// Add static file serving for each module's wwwroot directory
var rootPath = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location) ?? AppContext.BaseDirectory;
var modulesPath = Path.Combine(rootPath, "Modules");
if (Directory.Exists(modulesPath))
{
    foreach (var moduleDir in Directory.GetDirectories(modulesPath))
    {
        var moduleWwwRoot = Path.Combine(moduleDir, "wwwroot");
        if (Directory.Exists(moduleWwwRoot))
        {
            var moduleName = new DirectoryInfo(moduleDir).Name;
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(moduleWwwRoot),
                RequestPath = $"/Modules/{moduleName}"
            });
        }
    }
}

app.UseRouting();
app.UseAuthorization();

#endregion

#region API Endpoints

// Endpoint for receiving commands from the frontend
app.MapPost("/api/dispatch/{commandName}", async (
    string commandName,
    JsonElement payload,
    ICommandBus commandBus,
    CommandFactory commandFactory) =>
{
    var commandType = commandFactory.GetCommandType(commandName);
    if (commandType == null)
    {
        return Results.NotFound($"Command '{commandName}' not found.");
    }

    var traceContext = new TraceContext { Items = { ["ModuleId"] = "Host.Frontend" } };

    try
    {
        var command = payload.Deserialize(commandType, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (command == null) return Results.BadRequest("Could not deserialize command payload.");

        var resultInterface = commandType.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>));
        if (resultInterface != null)
        {
            var resultType = resultInterface.GetGenericArguments()[0];
            var dispatchMethod = typeof(ICommandBus).GetMethod(nameof(ICommandBus.Dispatch)).MakeGenericMethod(resultType);
            var task = (Task)dispatchMethod.Invoke(commandBus, new object[] { command, traceContext });
            await task;
            var result = task.GetType().GetProperty("Result")?.GetValue(task);
            return Results.Ok(result);
        }
        else
        {
            await commandBus.Dispatch((ICommand)command, traceContext);
            return Results.Ok();
        }
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.InnerException?.Message ?? ex.Message, statusCode: 500);
    }
});

// Endpoint for retrieving the service/command manifest
app.MapGet("/api/manifest", async (ICommandBus commandBus) =>
{
    try
    {
        var traceContext = new TraceContext { Items = { ["ModuleId"] = "Host.Frontend" } };
        var manifest = await commandBus.Dispatch<SoftwareCenter.Core.Discovery.RegistryManifest>(new GetRegistryManifestCommand(), traceContext);
        return Results.Ok(manifest);
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.InnerException?.Message ?? ex.Message, statusCode: 500);
    }
});

// Map controller routes and the SignalR hub
app.MapControllerRoute(name: "default", pattern: "{controller=Main}/{action=Index}/{id?}");
app.MapHub<UiHub>("/uihub");

#endregion

#region Application Initialization
// All initialization is now handled by IHostedService implementations.
// The ModuleLoader is an IHostedService registered by AddKernel().
// The HostFeatureUIService is also an IHostedService.
// They will be started automatically by the host.

// Launch browser
if (app.Environment.IsDevelopment())
{
    var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
    lifetime.ApplicationStarted.Register(() =>
    {
        var addresses = app.Services.GetRequiredService<IServerAddressesFeature>().Addresses;
        var url = addresses.FirstOrDefault(x => x.StartsWith("https://")) ?? addresses.FirstOrDefault();

        if (!string.IsNullOrEmpty(url))
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch(Exception ex)
            {
                var logger = app.Services.GetRequiredService<ILogger<Program>>();
                logger.LogError(ex, "Could not open browser.");
            }
        }
    });
}
#endregion

app.Run();