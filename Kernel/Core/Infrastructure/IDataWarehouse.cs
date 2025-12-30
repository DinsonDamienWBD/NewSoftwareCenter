using Core.Data;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;

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

        /// <summary>
        /// Saves a binary blob.
        /// </summary>
        Task StoreObjectAsync(string bucket, string key, Stream data, StorageIntent intent);

        /// <summary>
        /// Retrieves a binary blob.
        /// </summary>
        Task<Stream> RetrieveObjectAsync(string bucket, string key);

        /// <summary>
        /// Checks system health (Storage, Keys, Plugins).
        /// </summary>
        void CheckHealth();
    }
}