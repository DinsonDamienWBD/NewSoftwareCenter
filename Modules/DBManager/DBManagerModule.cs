using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SoftwareCenter.Core.Modules;
using System.Threading.Tasks;

namespace SoftwareCenter.Module.DBManager
{
    public class DBManagerModule : IModule
    {
        public string Id => "SoftwareCenter.DBManager";
        public string Name => "DB Manager";

        public void ConfigureServices(IServiceCollection services)
        {
            // Register SqlClientAdapter using configuration at resolve time
            services.AddTransient<Services.SqlClientAdapter>(sp =>
            {
                var config = sp.GetService<IConfiguration>();
                var conn = config?.GetConnectionString("DBManager") ?? config?["DBManager:ConnectionString"] ?? string.Empty;
                return new Services.SqlClientAdapter(conn);
            });

            // Register higher-level DB services that consume the adapter
            services.AddTransient<Services.ExampleDbService>();
        }

        public Task Initialize(System.IServiceProvider serviceProvider)
        {
            // No-op for now
            return Task.CompletedTask;
        }
    }
}
