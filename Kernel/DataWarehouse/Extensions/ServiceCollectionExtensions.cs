using Core.AI;
using Core.Infrastructure;
using DataWarehouse.Configuration;
using DataWarehouse.Contracts;
using DataWarehouse.Engine;
using DataWarehouse.Plugins;
using DataWarehouse.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DataWarehouse.Extensions
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
        public static IServiceCollection AddCosmicDataWarehouse(
            this IServiceCollection services,
            Action<DataWarehouseOptions> configure)
        {
            var options = new DataWarehouseOptions();
            configure(options);

            if (string.IsNullOrEmpty(options.RootPath)) throw new ArgumentException("RootPath required.");

            services.AddSingleton<PluginRegistry>();
            services.AddSingleton(sp => new RuntimeOptimizer(sp.GetRequiredService<ILogger<RuntimeOptimizer>>()));
            services.AddSingleton(sp => new PipelineOptimizer(sp.GetRequiredService<PluginRegistry>()));
            services.AddSingleton(sp => new FeatureManager(Path.Combine(options.RootPath, "Metadata")));

            services.AddSingleton<IKeyStore>(sp => new KeyStoreAdapter(options.RootPath));

            services.AddSingleton<IMetadataIndex>(sp =>
            {
                var optimizer = sp.GetRequiredService<RuntimeOptimizer>();
                bool useSqlite = (options.IndexType == IndexStorageType.Persistent) ||
                                 (options.IndexType == IndexStorageType.Auto && optimizer.ShouldUsePersistentIndex());

                if (useSqlite) return new SqliteMetadataIndex(Path.Combine(options.RootPath, "metadata.db"));
                return new InMemoryMetadataIndex();
            });

            services.AddSingleton<CosmicWarehouse>(sp =>
                new CosmicWarehouse(
                    sp.GetRequiredService<PluginRegistry>(),
                    sp.GetRequiredService<FeatureManager>(),
                    sp.GetRequiredService<ILogger<CosmicWarehouse>>(),
                    sp.GetRequiredService<ILoggerFactory>(), // FIX: Added Factory
                    sp.GetRequiredService<IKeyStore>(),
                    sp.GetRequiredService<IMetadataIndex>(),
                    sp.GetRequiredService<IMetricsProvider>(),
                    sp.GetRequiredService<RuntimeOptimizer>(),
                    sp.GetRequiredService<PipelineOptimizer>(),
                    options.RootPath
                ));

            services.AddSingleton<IDataWarehouse>(sp => sp.GetRequiredService<CosmicWarehouse>());

            return services;
        }
    }
}