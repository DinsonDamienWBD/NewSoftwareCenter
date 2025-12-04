using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using SoftwareCenter.Core.Commands;
using SoftwareCenter.Core.UI;
using SoftwareCenter.UI.Engine;
using SoftwareCenter.UI.Engine.Services;
using System.IO;

namespace SoftwareCenter.Host
{
    static class Program
    {
        [STAThread]
        static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // --- Service Registration ---
            builder.Services.AddSignalR();
            builder.Services.AddSingleton<IUIEngine, UIEngine>();
            
            // Create and register the Kernel instance
            var kernel = new StandardKernel();
            builder.Services.AddSingleton(kernel);

            var app = builder.Build();

            // --- Kernel and UI Engine Integration ---
            // After the app is built, retrieve the IUIEngine and register it with the Kernel's DI container.
            // This makes the IUIEngine available to all modules loaded by the Kernel.
            var uiEngine = app.Services.GetRequiredService<IUIEngine>();
            kernel.RegisterService<IUIEngine>(uiEngine);

            // --- API Endpoints ---
            app.MapPost("/api/ui/interact", async (InteractionRequest request, StandardKernel appKernel) =>
            {
                // The framework now handles deserialization and basic validation (e.g., required fields).
                var command = new UIInteractionCommand(request.OwnerId, request.Action, request.AdditionalData);
                var result = await appKernel.RouteAsync(command);

                // Return a generic success or failure. The module is responsible for any UI updates.
                return result.IsSuccess
                    ? Results.Ok(new { message = "Interaction processed." })
                    : Results.Problem(detail: result.ErrorMessage, statusCode: 500);
            });

            // --- Middleware Pipeline ---
            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                // Add production error handling (e.g., a generic error page)
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            app.UseDefaultFiles(); // Enables serving index.html for root requests
            app.UseStaticFiles(); // Serve files from wwwroot

            // Convention for serving module SPAs from the output directory
            var modulesPath = Path.Combine(app.Environment.ContentRootPath, "Modules");
            Directory.CreateDirectory(modulesPath); // Ensure it exists
            app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(modulesPath), RequestPath = "/modules" });

            app.MapHub<UIHub>("/ui-hub");

            // Start the Kernel (which will load modules)
            await kernel.StartAsync();

            await app.RunAsync();
        }
    }
}
