using Core.Frontend.Contracts;
using FrontendManager.Middleware;
using FrontendManager.Pipeline;
using FrontendManager.Registry;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace FrontendManager.Extensions
{
    /// <summary>
    /// Extension methods for integrating the Frontend Manager middleware into the ASP.NET Core pipeline.
    /// </summary>
    public static class FrontendManagerExtensions
    {
        /// <summary>
        /// Adds the Frontend Manager services to the IServiceCollection.
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddFrontendManager(this IServiceCollection services)
        {
            // 1. Registry
            services.AddSingleton<UiRegistry>();
            services.AddSingleton<IFrontendRegistry>(sp => sp.GetRequiredService<UiRegistry>());

            // 2. Middleware (Bus Governance - Injected into UiBus, NOT HTTP Pipeline)
            services.AddSingleton<UiExceptionMiddleware>();
            services.AddSingleton<AccessControlMiddleware>();
            services.AddSingleton<UiDeprecationMiddleware>();
            services.AddSingleton<UiValidationMiddleware>();
            services.AddSingleton<UiAuditMiddleware>();

            // 3. Pipeline
            services.AddSingleton<UiBus>();
            services.AddSingleton<IFrontendPipeline>(sp => sp.GetRequiredService<UiBus>());

            // 4. Services
            services.AddSingleton<FrontendManager.Services.IConnectionManager, FrontendManager.Services.ConnectionManager>();

            return services;
        }

        /// <summary>
        /// Uses the Frontend Manager middleware in the ASP.NET Core pipeline.
        /// </summary>
        /// <param name="app"></param>
        /// <returns></returns>
        public static IApplicationBuilder UseFrontendManager(this IApplicationBuilder app)
        {
            // Add the UI Shell server to the HTTP pipeline
            app.UseMiddleware<FrontendMiddleware>();

            // Warm up the Registry
            _ = app.ApplicationServices.GetService<IFrontendRegistry>();
            return app;
        }
    }
}