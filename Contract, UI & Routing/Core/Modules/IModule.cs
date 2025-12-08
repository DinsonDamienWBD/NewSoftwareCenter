using Microsoft.Extensions.DependencyInjection;

namespace SoftwareCenter.Core.Modules
{
    /// <summary>
    /// The primary contract for a loadable module.
    /// </summary>
    public interface IModule
    {
        /// <summary>
        /// Gets the unique, machine-readable ID of the module (e.g., "SoftwareCenter.AppManager").
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Gets the human-readable name of the module (e.g., "Application Manager").
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Allows the module to register its services (command handlers, etc.) with the application's
        /// dependency injection container.
        /// </summary>
        /// <param name="services">The service collection to add services to.</param>
        void ConfigureServices(IServiceCollection services);
    }
}
