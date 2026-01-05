using DataWarehouse.Kernel.Configuration;
using DataWarehouse.Kernel.Engine;
using DataWarehouse.Kernel.Indexing;
using DataWarehouse.Kernel.Security;
using DataWarehouse.SDK.Contracts;
using DataWarehouse.SDK.Security;
using DataWarehouse.SDK.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DataWarehouse.Kernel.Extensions
{
    /// <summary>
    /// Service collection extensions
    /// The "One-Line Install" for any .NET Host (ASP.NET, Console, Worker).
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Add data warehouse
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configure"></param>
        /// <returns></returns>
        public static IServiceCollection AddDataWarehouseDataWarehouse(
            this IServiceCollection services,
            Action<DataWarehouseOptions> configure)
        {
            var options = new DataWarehouseOptions();
            configure(options);

            if (string.IsNullOrEmpty(options.RootPath)) throw new ArgumentException("RootPath required.");

            // 1. Core Services
            services.AddSingleton<PluginRegistry>();

            // 2. Optimizers
            services.AddSingleton(sp => new RuntimeOptimizer(sp.GetRequiredService<ILogger<RuntimeOptimizer>>()));
            services.AddSingleton(sp => new PipelineOptimizer(
                sp.GetRequiredService<PluginRegistry>(),
                sp.GetRequiredService<IConfiguration>()
            ));

            // 3. Managers & Adapters
            services.AddSingleton(sp => new FeatureManager(Path.Combine(options.RootPath, "Metadata")));
            services.AddSingleton<IKeyStore>(sp => new KeyStoreAdapter(options.RootPath));

            // 4. Index Selection
            // Fix: We ONLY register InMemory here. Sqlite is now a Plugin.
            // If the Sqlite Plugin loads, it can register itself or replace this service.
            services.AddSingleton<IMetadataIndex>(sp => new InMemoryMetadataIndex());

            // 5. The Engine Kernel
            // Fix: Explicitly qualify DataWarehouse to avoid namespace collision
            services.AddSingleton<DataWarehouse.Kernel.Engine.DataWarehouse>(sp =>
                new DataWarehouse.Kernel.Engine.DataWarehouse(
                    options.RootPath,
                    sp.GetRequiredService<PluginRegistry>(),
                    sp.GetRequiredService<RuntimeOptimizer>(),
                    sp.GetRequiredService<PipelineOptimizer>(),
                    sp.GetRequiredService<IKeyStore>(),
                    sp.GetRequiredService<ILogger<DataWarehouse.Kernel.Engine.DataWarehouse>>()
                ));

            // 6. Public Interface Alias
            services.AddSingleton<IDataWarehouse>(sp => sp.GetRequiredService<DataWarehouse.Kernel.Engine.DataWarehouse>());

            return services;
        }
    }
}