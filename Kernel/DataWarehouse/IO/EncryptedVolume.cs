using Core.Data;
using Core.Primitives;
using Core.Security;

namespace DataWarehouse.IO
{
    /// <summary>
    /// Represents a "Secure Room" or "Compartment".
    /// Transparently encrypts all data written to it using a specific Room Key.
    /// </summary>
    public class EncryptedVolume(IStorageDriver backingStore, ICryptoAlgorithm crypto, byte[] roomKey, string roomPrefix) : IStorageDriver
    {
        private readonly IStorageDriver _backingStore = backingStore; // The physical disk driver
        private readonly ICryptoAlgorithm _crypto = crypto;
        private readonly byte[] _roomKey = roomKey;
        private readonly string _roomPrefix = roomPrefix; // e.g., "Modules/ModuleA/"

        /// <summary>
        /// Storage capabilities
        /// </summary>
        public StorageCapabilities Capabilities =>
            _backingStore.Capabilities | StorageCapabilities.NativeEncryption;

        // Helper to map relative "Room Path" to "Physical Warehouse Path"
        private string Qualify(string path) => Path.Combine(_roomPrefix, path);

        /// <summary>
        /// Save async
        /// </summary>
        /// <param name="path"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public async Task SaveAsync(string path, Stream data)
        {
            // 1. Read the input stream into memory (Encryption requires the whole block for GCM)
            // Note: For massive files (GBs), we would need a chunking strategy. 
            // For typical enterprise files (MBs), this is fine.
            using var ms = new MemoryStream();
            await data.CopyToAsync(ms);
            var plaintext = ms.ToArray();

            // 2. Encrypt
            var ciphertext = _crypto.Encrypt(plaintext, _roomKey);

            // 3. Write to backing store
            using var outStream = new MemoryStream(ciphertext);
            await _backingStore.SaveAsync(Qualify(path), outStream);
        }

        /// <summary>
        /// Load async
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public async Task<Stream> LoadAsync(string path)
        {
            // 1. Load Encrypted Blob
            using var encryptedStream = await _backingStore.LoadAsync(Qualify(path));
            using var ms = new MemoryStream();
            await encryptedStream.CopyToAsync(ms);
            var ciphertext = ms.ToArray();

            // 2. Decrypt
            // If the key is wrong, this throws CryptographicException (Access Denied)
            var plaintext = _crypto.Decrypt(ciphertext, _roomKey);

            return new MemoryStream(plaintext);
        }

        /// <summary>
        /// Delete async
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public Task DeleteAsync(string path) => _backingStore.DeleteAsync(Qualify(path));

        /// <summary>
        /// Check if exists async
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public Task<bool> ExistsAsync(string path) => _backingStore.ExistsAsync(Qualify(path));

        /// <summary>
        /// Get room details async
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public async Task<FileDetails> GetDetailsAsync(string path)
        {
            // We get details from the backing store, but the Size will be the Encrypted Size (slightly larger).
            // This is acceptable security trade-off to avoid decrypting just to check size.
            var details = await _backingStore.GetDetailsAsync(Qualify(path));
            details.Name = Path.GetFileName(path); // Hide prefix
            return details;
        }

        /// <summary>
        /// Save async in batch
        /// </summary>
        /// <param name="files"></param>
        /// <returns></returns>
        public Task SaveBatchAsync(IDictionary<string, Stream> files)
        {
            // Naive implementation loop. Optimizing this requires upstream batch encryption support.
            var tasks = new List<Task>();
            foreach (var f in files) tasks.Add(SaveAsync(f.Key, f.Value));
            return Task.WhenAll(tasks);
        }

        /// <summary>
        /// Delete async in batch
        /// </summary>
        /// <param name="paths"></param>
        /// <returns></returns>
        public Task DeleteBatchAsync(IEnumerable<string> paths)
        {
            var tasks = new List<Task>();
            foreach (var p in paths) tasks.Add(DeleteAsync(p));
            return Task.WhenAll(tasks);
        }

        /// <summary>
        /// Get checksum async
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public Task<string> GetChecksumAsync(string path) => _backingStore.GetChecksumAsync(Qualify(path));
    }
}