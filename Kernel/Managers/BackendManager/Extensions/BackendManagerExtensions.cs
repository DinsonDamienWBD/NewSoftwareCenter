using BackendManager.Middleware;
using BackendManager.Pipeline;
using BackendManager.Registry;
using BackendManager.Security;
using Core.Backend.Contracts;
using Core.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace BackendManager.Extensions
{
    /// <summary>
    /// Extension methods for integrating the Backend Manager into the ASP.NET Core pipeline.
    /// </summary>
    public static class BackendManagerExtensions
    {
        /// <summary>
        /// Adds Backend Manager services to the IServiceCollection.
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddBackendManager(this IServiceCollection services)
        {
            // 1. Registry (Singleton)
            services.AddSingleton<ServiceRegistry>();
            services.AddSingleton<IBackendRegistry>(sp => sp.GetRequiredService<ServiceRegistry>());

            // 2. Middleware (Transients/Scoped)
            services.AddTransient<ExceptionMiddleware>();
            services.AddTransient<ValidationMiddleware>();
            services.AddTransient<AuditMiddleware>();
            services.AddTransient<DeprecationMiddleware>();

            // 3. Security (AES + DPAPI)
            services.AddTransient<ICryptoProvider, DpapiCryptoProvider>();

            // 4. The Bus (Scoped per request, usually)
            services.AddScoped<IBackendPipeline, BackendBus>();

            return services;
        }

        /// <summary>
        /// Uses the Backend Manager middleware in the application pipeline.
        /// </summary>
        /// <param name="app"></param>
        /// <returns></returns>
        public static IApplicationBuilder UseBackendManager(this IApplicationBuilder app)
        {
            return app.UseMiddleware<BackendMiddleware>();
        }
    }
}