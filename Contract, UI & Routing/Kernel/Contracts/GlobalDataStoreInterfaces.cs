using System.Threading.Tasks;
using System;
using System.Collections.Generic;

namespace SoftwareCenter.Kernel.Contracts
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

        /// <summary>
        /// Stores a collection of data in a single transaction.
        /// </summary>
        /// <typeparam name="T">The type of data to store.</typeparam>
        /// <param name="items">A dictionary where the key is the identifier and the value is the payload.</param>
        /// <param name="policy">Transient (RAM) or Persistent (Disk).</param>
        /// <returns>True if the entire operation succeeds.</returns>
        Task<bool> StoreBulkAsync<T>(IDictionary<string, T> items, DataPolicy policy = DataPolicy.Transient);
    }
    
    /// <summary>
    /// Dictates how the Global Data Store handles persistence for a specific key.
    /// </summary>
    public enum DataPolicy
    {
        /// <summary>
        /// Stored in RAM only. Lost when the application closes.
        /// Use for: Session tokens, temporary UI state, caching.
        /// </summary>
        Transient = 0,

        /// <summary>
        /// Saved to disk (e.g., LiteDB/SQLite). Survives restarts.
        /// Use for: User settings, application config, long-term logs.
        /// </summary>
        Persistent = 1
    }

    /// <summary>
    /// A rich wrapper around stored data.
    /// Provides accountability (Who saved this?) and validity checks (When was this saved?).
    /// </summary>
    /// <typeparam name="T">The type of the payload.</typeparam>
    public class DataEntry<T>
    {
        /// <summary>
        /// The actual data payload.
        /// </summary>
        public T? Value { get; set; }

        /// <summary>
        /// UTC Timestamp of the last write operation.
        /// </summary>
        public DateTime LastUpdated { get; set; }

        /// <summary>
        /// The ID of the module that owns/wrote this data.
        /// </summary>
        public string SourceId { get; set; } = string.Empty;

        /// <summary>
        /// The fully qualified type name of the data (safety check for deserialization).
        /// </summary>
        public string DataType { get; set; } = string.Empty;

        /// <summary>
        /// The Trace ID of the command/operation that caused this data change.
        /// Links the data state back to the specific user action.
        /// </summary>
        public Guid TraceId { get; set; }
    }
}