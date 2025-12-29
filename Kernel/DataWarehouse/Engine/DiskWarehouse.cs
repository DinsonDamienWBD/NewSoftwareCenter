using Core.Contracts;
using Core.Data;
using Core.Diagnostics;
using Core.Infrastructure;
using Core.Security;
using DataWarehouse.IO;
using DataWarehouse.Security;
using DataWarehouse.Serialization;
using Microsoft.Extensions.Configuration;
using System.Collections.Concurrent;
using System.Runtime.Versioning;
using System.Security.Cryptography;

namespace DataWarehouse.Engine
{
    /// <summary>
    /// The concrete implementation of the Data Warehouse.
    /// This class is responsible for "Mounting" the disk, unlocking keys,
    /// and providing the operational drivers to the Core.
    /// </summary>
    [SupportedOSPlatform("windows")]
    
    public class DiskWarehouse : IDataWarehouse, IDisposable
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
            Dispose();
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
                var encrypted = new EncryptedVolume(_physicalDriver!, _crypto!, volumeKey, name);
                return new AuditLoggingDriver(encrypted, _logsPath, contextName: name);
            });
        }

        /// <summary>
        /// Health Probes
        /// </summary>
        /// <returns></returns>
        public IEnumerable<IProbe> GetProbes()
        {
            // Pass the Index to the probe to check for background errors
            yield return new WarehouseProbe(_rootPath, _masterKey != null, _index);
        }

        /// <summary>
        /// Security Hygiene
        /// </summary>
        public void Dispose()
        {
            _index?.Dispose();

            // Dispose all open volumes (AuditLoggingDriver needs to stop its thread)
            foreach (var vol in _openVolumes.Values)
            {
                if (vol is IDisposable d) d.Dispose();
            }
            _openVolumes.Clear();

            if (_masterKey != null)
            {
                CryptographicOperations.ZeroMemory(_masterKey);
                _masterKey = null;
            }
            GC.SuppressFinalize(this);
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

    /// <summary>
    /// Probe for health check
    /// </summary>
    public class WarehouseProbe(string path, bool unlocked, RamIndexDriver? index) : IProbe
    {
        private readonly string _path = path;
        private readonly bool _unlocked = unlocked;
        private readonly RamIndexDriver? _index = index;

        /// <summary>
        /// Name
        /// </summary>
        public string Name => "Data Warehouse";

        /// <summary>
        /// Run health check
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        public Task<HealthCheckResult> CheckHealthAsync(CancellationToken ct)
        {
            if (!_unlocked) return Task.FromResult(HealthCheckResult.Unhealthy("Warehouse Locked"));

            // Check if Index failed silently
            if (_index?.LastError != null)
            {
                return Task.FromResult(HealthCheckResult.Degraded("Index Persistence Failed", _index.LastError));
            }

            try
            {
                var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(_path)) ?? _path);
                if (drive.AvailableFreeSpace < 100 * 1024 * 1024)
                    return Task.FromResult(HealthCheckResult.Degraded("Low Disk Space"));

                return Task.FromResult(HealthCheckResult.Healthy());
            }
            catch (Exception ex)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("Storage Access Failed", ex));
            }
        }
    }
}