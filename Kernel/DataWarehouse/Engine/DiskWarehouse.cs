using Core.Contracts;
using Core.Data;
using Core.Infrastructure;
using Core.Security;
using DataWarehouse.IO;
using DataWarehouse.Security;
using DataWarehouse.Serialization;
using Microsoft.Extensions.Configuration;
using System.Collections.Concurrent;
using System.Runtime.Versioning;

namespace DataWarehouse.Engine
{
    /// <summary>
    /// The concrete implementation of the Data Warehouse.
    /// This class is responsible for "Mounting" the disk, unlocking keys,
    /// and providing the operational drivers to the Core.
    /// </summary>
    [SupportedOSPlatform("windows")]
    
    public class DiskWarehouse : IDataWarehouse
    {
        private string _rootPath = string.Empty;
        private string _logsPath = string.Empty;

        private LocalKeyStore? _keyStore;
        private IStorageDriver? _physicalDriver;
        private ISerializer? _serializer;
        private RamIndexDriver? _index;
        private AesGcmAlgorithm? _crypto;

        private byte[]? _masterKey;
        // The open rooms (cache of active volumes)
        private readonly ConcurrentDictionary<string, IStorageDriver> _openVolumes = new();

        // Exposed Services

        /// <summary>
        /// Get the serializer
        /// </summary>
        public ISerializer Serializer => _serializer ?? throw new InvalidOperationException("Not Mounted");

        /// <summary>
        /// Get the index driver
        /// </summary>
        public IIndexDriver Index => _index ?? throw new InvalidOperationException("Not Mounted");


        /// <summary>
        /// Configure warehouse path
        /// </summary>
        /// <param name="config"></param>
        public void Configure(IConfiguration config)
        {
            // Default to a folder named "Storage" next to the executable
            _rootPath = config["Warehouse:Path"] ?? Path.Combine(AppContext.BaseDirectory, "Storage");
            _logsPath = Path.Combine(_rootPath, "Logs");
        }

        /// <summary>
        /// Mount data warehouse
        /// </summary>
        /// <returns></returns>
        public async Task MountAsync()
        {
            Directory.CreateDirectory(_rootPath);
            Directory.CreateDirectory(_logsPath);

            // 1. Security
            _keyStore = new LocalKeyStore(_rootPath);
            _masterKey = _keyStore.LoadOrGenerateKey();
            _crypto = new AesGcmAlgorithm();

            // 2. IO & Serialization
            _physicalDriver = new LocalDiskDriver(_rootPath);
            _serializer = new JsonSerializerAdapter();

            // 3. Index (Hot Storage)
            _index = new RamIndexDriver(_rootPath, _serializer);
            await _index.RebuildAsync();
        }

        /// <summary>
        /// Dismount data warehouse
        /// </summary>
        /// <returns></returns>
        public Task DismountAsync()
        {
            _openVolumes.Clear();
            if (_masterKey != null) Array.Clear(_masterKey, 0, _masterKey.Length);
            return Task.CompletedTask;
        }

        /// <summary>
        /// MECHANISM: Opens a secure "Room" (Volume) using a specific key.
        /// The Kernel calls this when a Module requests access.
        /// </summary>
        public IStorageDriver OpenVolume(string volumeName, byte[] volumeKey)
        {
            return _openVolumes.GetOrAdd(volumeName, name =>
            {
                // 1. Create Encrypted View
                var encrypted = new EncryptedVolume(_physicalDriver!, _crypto!, volumeKey, name);

                // 2. Wrap in Auditor
                var audited = new AuditLoggingDriver(encrypted, _logsPath, contextName: name);

                return audited;
            });
        }

        // Key Management Extensions

        /// <summary>
        /// Set recovery password
        /// </summary>
        /// <param name="password"></param>
        public void SetRecoveryPassword(string password)
            => _keyStore?.SetRecoveryPassword(_masterKey!, password);


        /// <summary>
        /// Recover access if DPAPI is unable, using recovery password
        /// </summary>
        /// <param name="password"></param>
        public void RecoverAccess(string password)
            => _masterKey = _keyStore?.UnlockWithPassword(password);
    }
}