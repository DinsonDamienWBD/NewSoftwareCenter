using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using SoftwareCenter.Core;
using SoftwareCenter.Core.Commands;
using SoftwareCenter.Core.Diagnostics;
using SoftwareCenter.Core.UI;
using SoftwareCenter.Host.Controllers; // Implied for MainController
using SoftwareCenter.Host.Hubs;
using SoftwareCenter.Kernel;
using SoftwareCenter.Kernel.Services;
using SoftwareCenter.UIManager.Services;
using SoftwareCenter.Host.Services; // HostTemplateService
using SoftwareCenter.Core.Discovery.Commands; // GetRegistryManifestCommand
using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using SoftwareCenter.UIManager;

var builder = WebApplication.CreateBuilder(args);

// --- 1. Logging Configuration ---
builder.Logging.ClearProviders();
builder.Logging.AddProvider(new SoftwareCenter.Kernel.Logging.FileLoggerProvider(Path.Combine(AppContext.BaseDirectory, "Logs", "SoftwareCenter.log")));
builder.Logging.AddConsole();

// --- 2. Module Loader Setup (Pre-Container Build) ---
var tempServices = new ServiceCollection();
// Register Kernel services so modules can access core types during ConfigureModuleServices
tempServices.AddKernel();
tempServices.AddUIManager();

#pragma warning disable ASP0000
var tempProvider = tempServices.BuildServiceProvider();
#pragma warning restore ASP0000

var tempErrorHandler = tempProvider.GetRequiredService<SoftwareCenter.Core.Errors.IErrorHandler>();
var tempRoutingRegistry = tempProvider.GetRequiredService<IServiceRoutingRegistry>();
var tempServiceRegistry = tempProvider.GetRequiredService<IServiceRegistry>();

var moduleLoader = new ModuleLoader(tempErrorHandler, tempRoutingRegistry, tempServiceRegistry);
// Let modules register their own services into the real builder.Services; Host does not reference module types
moduleLoader.ConfigureModuleServices(builder.Services);

if (tempProvider is IDisposable disposableProvider)
{
    disposableProvider.Dispose();
}

// --- 3. Core Service Registration ---
builder.Services.AddKernel();
builder.Services.AddControllers();
builder.Services.AddSignalR();

// --- 4. UI Service Registration (New Architecture) ---
// Register UIManager and Host-provided template service per metadata
builder.Services.AddUIManager();
builder.Services.AddSingleton<ITemplateService, HostTemplateService>();

// Register the Renderer which handles the Manifest logic and Index composition
builder.Services.AddScoped<IUiService, UiRenderer>();

var app = builder.Build();

// --- 5. API Endpoints (Command Dispatcher) ---
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

    // TODO: The ModuleId should come from an authenticated user's claims or a request header.
    var traceContext = new TraceContext { Items = { ["ModuleId"] = "Host.Frontend" } };

    try
    {
        var command = payload.Deserialize(commandType, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (command == null) return Results.BadRequest("Could not deserialize command payload.");

        // Check if the command returns a result (ICommand<T>)
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

// --- Manifest endpoint ---
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

// --- 6. Pipeline Configuration ---
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// IMPORTANT: Removed app.UseDefaultFiles().
// We want the root "/" to be handled by MainController.Index, not by serving a static file.
app.UseStaticFiles();

app.UseRouting();
app.UseAuthorization();

// --- 7. Module Static Files ---
var rootPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
var modulesPath = Path.Combine(rootPath, "Modules");
if (Directory.Exists(modulesPath))
{
    var moduleDirectories = Directory.GetDirectories(modulesPath);
    foreach (var moduleDir in moduleDirectories)
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

// --- 8. Endpoint Mapping ---
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Main}/{action=Index}/{id?}");

// Map the new SignalR Hub
app.MapHub<UiHub>("/uihub");

// --- 9. System Initialization ---
using (var scope = app.Services.CreateScope())
{
    var serviceProvider = scope.ServiceProvider;

    // Initialize Modules (Backend Logic)
    var loader = serviceProvider.GetRequiredService<ModuleLoader>();
    await loader.InitializeModules(serviceProvider);
}

app.Run();