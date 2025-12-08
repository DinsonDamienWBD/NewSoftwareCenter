using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.SignalR;
using SoftwareCenter.Core.Commands;
using SoftwareCenter.Core.Diagnostics;
using SoftwareCenter.Kernel;
using SoftwareCenter.Kernel.Services;
using SoftwareCenter.UIManager;
using SoftwareCenter.UIManager.Services;
using SoftwareCenter.Host; // For UIHub

var builder = WebApplication.CreateBuilder(args);

// --- Dependency Injection ---
builder.Services.AddKernel();
builder.Services.AddUIManager();
builder.Services.AddSignalR(); // Add SignalR services

var app = builder.Build();

// --- Wire up SignalR to UIManager events ---
var uiStateService = app.Services.GetRequiredService<UIStateService>();
var uiHubContext = app.Services.GetRequiredService<IHubContext<UIHub>>();

uiStateService.UIStateChanged += async () =>
{
    var uiState = uiStateService.GetAllElements();
    await uiHubContext.Clients.All.SendAsync("ReceiveUIUpdate", uiState);
};

// --- API Endpoints ---
app.MapGet("/api/ui_state", (UIStateService uiStateService) =>
{
    return Results.Ok(uiStateService.GetAllElements());
});

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

    try
    {
        var command = payload.Deserialize(commandType, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (command == null) return Results.BadRequest("Could not deserialize command payload.");
        
        var traceContext = new TraceContext { Items = { ["ModuleId"] = "Host.Frontend" } };

        var resultInterface = commandType.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>));
        if (resultInterface != null)
        {
            var resultType = resultInterface.GetGenericArguments()[0];
            var dispatchMethod = typeof(ICommandBus).GetMethod(nameof(ICommandBus.Dispatch), new[] { typeof(ICommand<>).MakeGenericType(resultType), typeof(ITraceContext) });
            
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


// --- Frontend Hosting ---
app.UseDefaultFiles();
app.UseStaticFiles();

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

app.MapHub<UIHub>("/uihub"); // Map the SignalR hub

app.Run();