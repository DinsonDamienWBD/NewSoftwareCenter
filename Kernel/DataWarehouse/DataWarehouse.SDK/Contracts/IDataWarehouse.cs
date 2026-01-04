using DataWarehouse.SDK.Security;

namespace DataWarehouse.SDK.Contracts
{
    public interface IDataWarehouse
    {
        // --- Storage Operations (Secured) ---

        /// <summary>
        /// Stores a blob in a specific container with security checks.
        /// </summary>
        Task<string> StoreBlobAsync(
            ISecurityContext context,
            string containerId,
            string blobName,
            Stream data);

        /// <summary>
        /// Retrieves a blob if the context has Read access.
        /// </summary>
        Task<Stream> GetBlobAsync(
            ISecurityContext context,
            string containerId,
            string blobName);

        // --- Management Operations ---

        /// <summary>
        /// Creates a new container (Room/Partition) and assigns ownership to the caller.
        /// </summary>
        Task CreateContainerAsync(
            ISecurityContext context,
            string containerId,
            bool encrypt = false,
            bool compress = false);

        /// <summary>
        /// Updates the ACL for a container.
        /// </summary>
        Task GrantAccessAsync(
            ISecurityContext ownerContext,
            string containerId,
            string targetUserId,
            AccessLevel level);

        // --- Search Operations ---

        Task<string[]> SearchAsync(
            ISecurityContext context,
            string query,
            float[]? vector,
            int limit = 10);

        // Original IDataWarehouse Contract from Core.Contracts
        /*
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
        */
    }
}