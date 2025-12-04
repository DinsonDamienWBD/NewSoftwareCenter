using System.Threading.Tasks;

namespace SoftwareCenter.Core.Data
{
    /// <summary>
    /// The contract for the system's "Synapse" (Shared Memory).
    /// Abstracts away the physical storage (RAM vs Disk/DB).
    /// </summary>
    public interface IGlobalDataStore
    {
        /// <summary>
        /// Stores data with a specific persistence policy.
        /// </summary>
        /// <typeparam name="T">The type of data to store.</typeparam>
        /// <param name="key">Unique identifier (e.g., "User.Theme").</param>
        /// <param name="data">The payload.</param>
        /// <param name="policy">Transient (RAM) or Persistent (Disk).</param>
        /// <returns>True if successful.</returns>
        Task<bool> StoreAsync<T>(string key, T data, DataPolicy policy = DataPolicy.Transient);

        /// <summary>
        /// Retrieves the data along with its metadata (timestamp, source).
        /// </summary>
        /// <returns>The wrapped data, or null if not found.</returns>
        Task<DataEntry<T>?> RetrieveAsync<T>(string key);

        /// <summary>
        /// Checks if a key exists without incurring the cost of deserializing the payload.
        /// </summary>
        Task<bool> ExistsAsync(string key);

        /// <summary>
        /// Removes a specific key from storage.
        /// </summary>
        Task<bool> RemoveAsync(string key);

        /// <summary>
        /// Retrieves only the metadata (Header) without loading the full payload.
        /// Useful for checking "LastUpdated" or "SourceId" on large objects.
        /// </summary>
        Task<DataEntry<object>?> GetMetadataAsync(string key);
    }
}