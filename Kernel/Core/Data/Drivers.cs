using Core.Primitives;

namespace Core.Data
{
    /// <summary>
    /// Details about a stored file.
    /// </summary>
    public class FileDetails
    {
        /// <summary>
        /// File name with extension.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// File size in bytes.
        /// </summary>
        public long SizeBytes { get; set; }

        /// <summary>
        /// File MIME type.
        /// </summary>
        public string? ContentType { get; set; }

        /// <summary>
        /// File hash for integrity verification.
        /// </summary>
        public string? Hash { get; set; } // ETag or SHA256

        /// <summary>
        /// Last modified timestamp.
        /// </summary>
        public DateTime LastModified { get; set; }
    }

    /// <summary>
    /// Contract for the Physical Storage Mechanism (Disk/Cloud).
    /// </summary>
    public interface IStorageDriver
    {
        /// <summary>
        /// Saves a file to the storage backend.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        Task SaveAsync(string path, Stream data);

        /// <summary>
        /// Loads a file from the storage backend.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        Task<Stream> LoadAsync(string path);

        /// <summary>
        /// Deletes a file from the storage backend.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        Task DeleteAsync(string path);

        /// <summary>
        /// Checks if a file exists in the storage backend.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        Task<bool> ExistsAsync(string path);

        /// <summary>Optimized batch save for bulk imports.</summary>
        Task SaveBatchAsync(IDictionary<string, Stream> files);

        /// <summary>Optimized batch delete.</summary>
        Task DeleteBatchAsync(IEnumerable<string> paths);

        /// <summary>
        /// Retrieves detailed metadata about a stored file.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        Task<FileDetails> GetDetailsAsync(string path);

        /// <summary>
        /// Data Integrity: Gets the checksum of a file for integrity verification.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        Task<string> GetChecksumAsync(string path);

        /// <summary>Capability flags for pipeline optimization.</summary>
        StorageCapabilities Capabilities { get; }
    }

    /// <summary>
    /// Contract for the High-Speed Metadata Index (RAM/Search).
    /// </summary>
    public interface IIndexDriver
    {
        /// <summary>
        /// Upserts (inserts or updates) a metadata entry in the index.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="metadata"></param>
        /// <returns></returns>
        Task UpsertEntryAsync(string id, Dictionary<string, string> metadata);

        /// <summary>
        /// Deletes a metadata entry from the index.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        Task RemoveEntryAsync(string id);

        /// <summary>Performs a search query against metadata tags.</summary>
        Task<IEnumerable<string>> SearchAsync(string query);

        /// <summary>Triggers a full index rebuild from source storage.</summary>
        Task RebuildAsync();
    }
}