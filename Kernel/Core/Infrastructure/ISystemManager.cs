using Core.Pipeline;
using Microsoft.Extensions.Configuration;

namespace Core.Infrastructure
{
    /// <summary>
    /// INFRASTRUCTURE CONTRACT.
    /// Represents the "Brain" or "Manager" of the application.
    /// The Host uses this to boot the system and access the main pipeline.
    /// </summary>
    public interface ISystemManager
    {
        /// <summary>
        /// Provides access to the message processing highway.
        /// The Host (API/UI) uses this to dispatch Commands and Queries.
        /// </summary>
        IPipeline Pipeline { get; }

        /// <summary>
        /// Configures the Kernel settings (called during Host startup).
        /// </summary>
        void Configure(IConfiguration config);

        /// <summary>
        /// Boots the system: Mounts the Data Warehouse, Loads Modules, and Starts Background Jobs.
        /// </summary>
        Task StartAsync();

        /// <summary>
        /// Gracefully stops the system: Stops Jobs, Unloads Modules, and Dismounts Storage.
        /// </summary>
        Task StopAsync();
    }
}