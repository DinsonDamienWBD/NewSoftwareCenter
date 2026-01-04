using Core;
using Core.Backend.Contracts;
using Core.Frontend.Contracts;
using Core.Frontend.Messages;
using Core.Log;
using Core.Modules.Contracts;
using Core.ServiceRegistry;
using Core.Services;
using DataWarehouse;
using DataWarehouse.Kernel.Contracts.Messages;
using FrontendManager.Extensions;
using FrontendManager.Handlers;
using FrontendManager.Services;
using Host.Infrastructure;
using Host.Modules;
using Host.Services;
using Manager.Extensions;
using Manager.Pipeline;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.FileProviders;
using System.Diagnostics;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

// 1. Setup Configuration & Paths
// In V1, we hardcode or read from appsettings. If missing, we default to "Data" folder.
var dataPath = builder.Configuration["DataWarehousePath"]
               ?? Path.Combine(AppContext.BaseDirectory, "Data");

builder.Services.AddSingleton<ISmartLogger, HostSmartLogger>();
builder.Services.AddSingleton<IAuditLogger, HostAuditLogger>();

builder.Services.AddSingleton<IDocumentationProvider>(sp =>
{
    var logger = sp.GetRequiredService<ISmartLogger>();
    var provider = new XmlDocumentationProvider(logger);
    provider.LoadFromDirectory(AppContext.BaseDirectory);
    return provider;
});

// 2. Infrastructure Layer (The Kernel)
// Note: We use the extension methods we built in Phase 1-3
builder.Services.AddDataWarehouse(dataPath);
builder.Services.AddBackendManager();
builder.Services.AddFrontendManager();

builder.Services.AddSingleton<IFileSystemProvider, LocalFileSystemProvider>();
builder.Services.AddSingleton<INotificationService, UiNotificationService>();
builder.Services.AddSingleton<IConnectionManager, ConnectionManager>();

// FIX: Add SignalR for "Live Wire"
builder.Services.AddSignalR();

// OVERRIDE: Force Bus to be Singleton to allow Root resolution by Singleton Registry
builder.Services.AddSingleton<IBackendPipeline, BackendBus>();

builder.Services.AddTransient<UiCompositionHandler>();
builder.Services.AddTransient<IHandler<InjectUiCommand, Result<List<string>>>, UiCompositionHandler>();

// --- Register System Handlers Explicitly ---
// Because SystemHandlers is part of the Host, we must register it in DI 
// so the BackendBus can find it when SystemModule registers the routes.
builder.Services.AddTransient<SystemHandlers>();

// We must also register the interfaces it implements, so the Bus can resolve them directly
builder.Services.AddTransient<IHandler<NavigateCommand>, SystemHandlers>();
builder.Services.AddTransient<IHandler<PowerCommand>, SystemHandlers>();
builder.Services.AddTransient<IHandler<GetSettingsQuery, Result<SystemSettingsModel>>, SystemHandlers>();
builder.Services.AddTransient<IHandler<SaveSettingsCommand, Result<SystemSettingsModel>>, SystemHandlers>();
// 1. Register the Concrete Class
builder.Services.AddTransient<DeveloperHandler>();

// 2. FIX: Register the Interface mapping so the Bus can find it
builder.Services.AddTransient<IHandler<GetRegistryHelpQuery, Result<List<RegistryItemMetadata>>>, DeveloperHandler>();

// 3. Host Services (The Glue)
builder.Services.AddSingleton<ModuleLoader>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// 4. The Handshake (Module Loading)
// We do this BEFORE the server starts accepting requests
using (var scope = app.Services.CreateScope())
{
    var loader = scope.ServiceProvider.GetRequiredService<ModuleLoader>();
    var backend = scope.ServiceProvider.GetRequiredService<IBackendRegistry>();
    var frontend = scope.ServiceProvider.GetRequiredService<IFrontendRegistry>();

    // Scan and Initialize all modules
    await loader.LoadModulesAsync(backend, frontend);

    // B. MANUALLY LOAD INTERNAL MODULES

    // 1. Developer Module
    var devModule = new Host.Modules.DeveloperModule();
    await devModule.InitializeAsync(new ModuleContext { ModulePath = AppContext.BaseDirectory });
    devModule.Register(backend, frontend);
    Console.WriteLine($"[Host] Manually Loaded: {devModule.ModuleName}");

    // 2. FIX: System Module (Required for Shell & Navigation)
    var sysModule = new Host.Modules.SystemModule();
    await sysModule.InitializeAsync(new ModuleContext { ModulePath = AppContext.BaseDirectory });
    sysModule.Register(backend, frontend);
    Console.WriteLine($"[Host] Manually Loaded: {sysModule.ModuleName}");
}

// 5. The Pipeline (Web Server)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

var assetsPath = Path.Combine(AppContext.BaseDirectory, "Assets");
if (!Directory.Exists(assetsPath)) Directory.CreateDirectory(assetsPath);

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(assetsPath),
    RequestPath = "" // Serves directly at root, e.g. /developer.html
});

// FIX: Map SignalR Hub
app.MapHub<FrontendManager.Hubs.FrontendHub>("/frontendHub");

// 1. The Traffic Cop (Unified Dispatch)
// It rewrites /api/dispatch -> /api/backend/dispatch OR /api/frontend/dispatch
app.UseMiddleware<UnifiedDispatchMiddleware>();

// 2. The Managers (They listen for their specific paths)
app.UseFrontendManager();
app.UseBackendManager();

app.UseAuthorization();
app.MapControllers();

// 6. Auto-Launch Browser Logic
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();

lifetime.ApplicationStarted.Register(() =>
{
    var server = app.Services.GetRequiredService<IServer>();
    var addressFeature = server.Features.Get<IServerAddressesFeature>();

    if (addressFeature != null)
    {
        foreach (var address in addressFeature.Addresses)
        {
            // We only need to launch once
            Console.WriteLine($"[Host] Listening on: {address}");
            LaunchBrowser(address);
            return;
        }
    }
});

// 7. Launch
app.Run();

// Example usage of DataWarehouse
// The Host doesn't need to know about 'DataWarehouseWarehouse' class or 'IStorageProvider'.
// It just creates a Command DTO.

var cmd = new StoreBlobCommand
{
    Bucket = "invoices",
    Key = "2025-01.pdf",
    Data = fileStream
};

// Fire and Forget (or Await)
var result = await _bus.Send(cmd);


// User writes this line only.
services.AddDataWarehouseDataWarehouse(opts => opts.RootPath = "C:/MyData");

// Result:
// RuntimeOptimizer detects "Laptop" -> Checks container status.
// Result -> Uses SQLite (Persistent) so tags aren't lost on reboot.

// User wants a super-fast cache that wipes on restart.
services.AddDataWarehouseDataWarehouse(opts =>
{
    opts.RootPath = "/mnt/ramdisk";
    opts.IndexType = IndexStorageType.Volatile; // Force Speed
});

// Running in Kubernetes.
services.AddDataWarehouseDataWarehouse(opts => opts.RootPath = "/data");

// Result:
// RuntimeOptimizer sees DOTNET_RUNNING_IN_CONTAINER = true.
// Result -> Uses InMemory (Volatile) to keep the container image light and fast.



// --- HELPER FUNCTION ---
static void LaunchBrowser(string url)
{
    try
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true // Required on Windows to open default browser
        });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Host] Failed to launch browser: {ex.Message}");
    }
}