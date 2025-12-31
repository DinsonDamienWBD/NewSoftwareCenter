using DataWarehouse.Primitives;

namespace DataWarehouse.Contracts
{
    /// <summary>
    /// DataWarehouse plugin interface
    /// </summary>
    public interface IPlugin
    {
        /// <summary>
        /// Plugin ID
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Plugin version
        /// </summary>
        string Version { get; }
    }

    /// <summary>
    /// 1. Storage Providers (The "Limbs")
    /// </summary>
    public interface IStorageProvider : IPlugin
    {
        /// <summary>
        /// Storage scheme
        /// "file", "s3", "ram"
        /// </summary>
        string Scheme { get; }

        /// <summary>
        /// Save data
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        Task SaveAsync(Uri uri, Stream data);

        /// <summary>
        /// Load data
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        Task<Stream> LoadAsync(Uri uri);

        /// <summary>
        /// Delete data
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        Task DeleteAsync(Uri uri);

        /// <summary>
        /// Check if data exists
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        Task<bool> ExistsAsync(Uri uri);
    }

    /// <summary>
    /// 2. Crypto Providers (The "Shields")
    /// </summary>
    public interface ICryptoProvider : IPlugin
    {
        /// <summary>
        /// Level of security
        /// </summary>
        SecurityLevel Level { get; }

        /// <summary>
        /// Encrypt data
        /// </summary>
        /// <param name="data"></param>
        /// <param name="key"></param>
        /// <param name="nonce"></param>
        /// <param name="aad"></param>
        /// <returns></returns>
        byte[] Encrypt(byte[] data, byte[] key, byte[] nonce, byte[] aad);
        
        /// <summary>
        /// Decrypt data
        /// </summary>
        /// <param name="data"></param>
        /// <param name="key"></param>
        /// <param name="nonce"></param>
        /// <param name="aad"></param>
        /// <returns></returns>
        byte[] Decrypt(byte[] data, byte[] key, byte[] nonce, byte[] aad);

        /// <summary>
        /// Nonce size
        /// </summary>
        int NonceSize { get; }

        /// <summary>
        /// Tag size
        /// </summary>
        int TagSize { get; }
    }

    /// <summary>
    /// 3. Compression Providers (The "Compressors")
    /// </summary>
    public interface ICompressionProvider : IPlugin
    {
        /// <summary>
        /// Level of compression
        /// </summary>
        CompressionLevel Level { get; }

        /// <summary>
        /// Compress data
        /// </summary>
        /// <param name="output"></param>
        /// <returns></returns>
        Stream CreateCompressionStream(Stream output);

        /// <summary>
        /// Decompress data
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        Stream CreateDecompressionStream(Stream input);
    }

    /// <summary>
    /// Search Provider for Semantic Memory
    /// </summary>
    public interface IMetadataIndex : IPlugin
    {
        /// <summary>
        /// Index the Manifest 
        /// </summary>
        /// <param name="manifest"></param>
        /// <returns></returns>
        Task IndexManifestAsync(Primitives.Manifest manifest);

        /// <summary>
        /// Search
        /// </summary>
        /// <param name="query"></param>
        /// <param name="vector"></param>
        /// <param name="limit"></param>
        /// <returns></returns>
        Task<string[]> SearchAsync(string query, float[]? vector, int limit);

        /// <summary>
        /// Required for DataVacuum and Vector Cache Rehydration
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        IAsyncEnumerable<Manifest> EnumerateAllAsync(CancellationToken ct = default);
    }

    /// <summary>
    /// AI CONTRACT.
    /// Defines how an Autonomous Agent interacts with the Warehouse.
    /// Methods are designed to be "Function Calling" friendly (String I/O).
    /// </summary>
    public interface IAgentStorageTools
    {
        /// <summary>
        /// "Memorize this information securely."
        /// </summary>
        Task<string> StoreMemoryAsync(string content, string[] tags, string summary);

        /// <summary>
        /// "Recall information about X."
        /// </summary>
        Task<string> RecallMemoryAsync(string memoryId);

        /// <summary>
        /// "Find related memories." (Semantic Search placeholder)
        /// </summary>
        Task<string[]> SearchMemoriesAsync(float[] vector, int limit = 5);
    }
}