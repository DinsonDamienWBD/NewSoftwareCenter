using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace Core.Infrastructure
{
    /// <summary>
    /// INFRASTRUCTURE CONTRACT.
    /// Implemented ONLY by the DataWarehouse project.
    /// Consumed ONLY by the Host/Kernel for startup lifecycle.
    /// Modules MUST NOT see this interface.
    /// </summary>
    public interface IDataWarehouse
    {
        /// <summary>
        /// Configure the data warehouse with settings from configuration.
        /// </summary>
        /// <param name="config"></param>
        void Configure(IConfiguration config);

        /// <summary>
        /// Mount the data warehouse.
        /// </summary>
        /// <returns></returns>
        Task MountAsync();

        /// <summary>
        /// Unmount the data warehouse.
        /// </summary>
        /// <returns></returns>
        Task DismountAsync();
    }
}