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

        // Header: [Version 1b] [AlgId 1b]
        private const byte CurrentVersion = 0x01;
        private const int ChunkSize = 1024 * 1024; // 1MB

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
            using var outStream = new MemoryStream();
            outStream.WriteByte(CurrentVersion);

            // Use Async Chunked Stream
            using (var encryptor = new ChunkedEncryptionStream(outStream, _crypto, _roomKey))
            {
                await data.CopyToAsync(encryptor);
                // Dispose will trigger FlushAsync
            }

            outStream.Position = 0;
            await _backingStore.SaveAsync(Qualify(path), outStream);
        }

        /// <summary>
        /// Load async
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public async Task<Stream> LoadAsync(string path)
        {
            using var encryptedStream = await _backingStore.LoadAsync(Qualify(path));
            var outStream = new MemoryStream();

            // 1. Verify Header
            int version = encryptedStream.ReadByte();
            if (version == -1)
            {
                // FIX: Zero-byte file (Empty). Return empty stream.
                return new MemoryStream();
            }
            if (version != CurrentVersion) throw new InvalidOperationException($"Unknown file version: {version}");

            // 2. Read Chunks
            var lengthBuffer = new byte[4];
            while (await encryptedStream.ReadAsync(lengthBuffer.AsMemory(0, 4)) > 0)
            {
                var blockLength = BitConverter.ToInt32(lengthBuffer, 0);
                if (blockLength == 0) continue; // Safety check

                var block = new byte[blockLength];
                var read = await encryptedStream.ReadAsync(block.AsMemory(0, blockLength));
                if (read != blockLength) throw new EndOfStreamException();

                var plaintext = _crypto.Decrypt(block, _roomKey);
                await outStream.WriteAsync(plaintext.AsMemory());
            }

            outStream.Position = 0;
            return outStream;
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