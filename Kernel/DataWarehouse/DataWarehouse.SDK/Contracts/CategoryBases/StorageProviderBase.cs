using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DataWarehouse.SDK.Contracts.CategoryBases
{
    /// <summary>
    /// Base class for all Storage Provider plugins.
    /// Handles physical data persistence to various storage backends.
    ///
    /// Storage providers are filesystem-agnostic (Windows/Linux/Mac/ZFS) and
    /// hardware-agnostic (tapes/floppies/CDs/HDDs/SSDs/NVMe/network/cloud).
    ///
    /// Common CRUD operations (save/load/delete/exists/list) are implemented once.
    /// Plugins only implement the backend-specific read/write logic.
    ///
    /// Features handled by base class:
    /// - Auto-grow/shrink storage allocation
    /// - Path sanitization (cross-platform)
    /// - Data format conversion (byte[]/Stream/base64)
    /// - Standard CRUD capabilities
    ///
    /// Plugins only implement:
    /// 1. StorageType property (e.g., "local", "s3", "ipfs")
    /// 2. MountInternalAsync() - Connect to storage
    /// 3. ReadBytesAsync() - Physical read
    /// 4. WriteBytesAsync() - Physical write
    /// 5. DeleteBytesAsync() - Physical delete
    /// 6. ExistsBytesAsync() - Check existence
    /// 7. ListKeysAsync() - List keys
    /// </summary>
    public abstract class StorageProviderBase : PluginBase
    {
        /// <summary>Current storage size in bytes</summary>
        private long _currentSize;
        /// <summary>Reserved storage capacity in bytes</summary>
        private long _reservedSize;

        /// <summary>
        /// Constructs a storage provider with specified metadata.
        /// Automatically sets category to Storage.
        /// </summary>
        protected StorageProviderBase(string id, string name, Version version)
            : base(id, name, version, PluginCategory.Storage)
        {
        }

        // =========================================================================
        // ABSTRACT MEMBERS - Plugin must implement
        // =========================================================================

        /// <summary>Storage type identifier (e.g., "local", "s3", "ipfs")</summary>
        protected abstract string StorageType { get; }

        /// <summary>Mount/connect to storage backend</summary>
        protected abstract Task MountInternalAsync(IKernelContext context);

        /// <summary>Unmount/disconnect from storage backend</summary>
        protected abstract Task UnmountInternalAsync();

        /// <summary>Physically read bytes from storage</summary>
        protected abstract Task<byte[]> ReadBytesAsync(string key);

        /// <summary>Physically write bytes to storage</summary>
        protected abstract Task WriteBytesAsync(string key, byte[] data);

        /// <summary>Physically delete from storage</summary>
        protected abstract Task DeleteBytesAsync(string key);

        /// <summary>Check if key exists in storage</summary>
        protected abstract Task<bool> ExistsBytesAsync(string key);

        /// <summary>List keys with optional prefix</summary>
        protected abstract Task<List<string>> ListKeysAsync(string prefix);

        // =========================================================================
        // VIRTUAL MEMBERS - Plugin can override
        // =========================================================================

        /// <summary>Initial reserved size (0 = unlimited)</summary>
        protected virtual long InitialReservedSize => 0;

        /// <summary>Custom storage initialization (optional)</summary>
        protected virtual void InitializeStorage(IKernelContext context) { }

        // =========================================================================
        // CAPABILITIES - Auto-declared CRUD operations
        // =========================================================================

        /// <summary>Declares standard CRUD capabilities for this storage provider</summary>
        protected override PluginCapabilityDescriptor[] Capabilities => new[]
        {
            new PluginCapabilityDescriptor
            {
                CapabilityId = $"storage.{StorageType}.save",
                DisplayName = "Save Data",
                Description = $"Save data to {StorageType} storage",
                Category = CapabilityCategory.Storage,
                RequiredPermission = Security.Permission.Write,
                RequiresApproval = false,
                Tags = new List<string> { "storage", "save", "write", StorageType }
            },
            new PluginCapabilityDescriptor
            {
                CapabilityId = $"storage.{StorageType}.load",
                DisplayName = "Load Data",
                Description = $"Load data from {StorageType} storage",
                Category = CapabilityCategory.Storage,
                RequiredPermission = Security.Permission.Read,
                RequiresApproval = false,
                Tags = new List<string> { "storage", "load", "read", StorageType }
            },
            new PluginCapabilityDescriptor
            {
                CapabilityId = $"storage.{StorageType}.delete",
                DisplayName = "Delete Data",
                Description = $"Delete data from {StorageType} storage",
                Category = CapabilityCategory.Storage,
                RequiredPermission = Security.Permission.Delete,
                RequiresApproval = true, // Delete requires approval
                Tags = new List<string> { "storage", "delete", StorageType }
            },
            new PluginCapabilityDescriptor
            {
                CapabilityId = $"storage.{StorageType}.exists",
                DisplayName = "Check Existence",
                Description = $"Check if data exists in {StorageType} storage",
                Category = CapabilityCategory.Storage,
                RequiredPermission = Security.Permission.Read,
                RequiresApproval = false,
                Tags = new List<string> { "storage", "exists", StorageType }
            },
            new PluginCapabilityDescriptor
            {
                CapabilityId = $"storage.{StorageType}.list",
                DisplayName = "List Keys",
                Description = $"List keys in {StorageType} storage",
                Category = CapabilityCategory.Storage,
                RequiredPermission = Security.Permission.Read,
                RequiresApproval = false,
                Tags = new List<string> { "storage", "list", StorageType }
            }
        };

        // =========================================================================
        // INITIALIZATION - Registers CRUD handlers
        // =========================================================================

        /// <summary>
        /// Initializes storage provider (IMPLEMENTED ONCE for all storage plugins).
        /// Mounts storage and registers CRUD handlers automatically.
        /// </summary>
        protected override void InitializeInternal(IKernelContext context)
        {
            _reservedSize = InitialReservedSize;
            _currentSize = 0;

            // Mount storage backend
            MountInternalAsync(context).GetAwaiter().GetResult();

            // Register CRUD handlers with common logic
            RegisterCapability($"storage.{StorageType}.save", HandleSaveAsync);
            RegisterCapability($"storage.{StorageType}.load", HandleLoadAsync);
            RegisterCapability($"storage.{StorageType}.delete", HandleDeleteAsync);
            RegisterCapability($"storage.{StorageType}.exists", HandleExistsAsync);
            RegisterCapability($"storage.{StorageType}.list", HandleListAsync);

            // Plugin-specific initialization
            InitializeStorage(context);
        }

        // =========================================================================
        // CRUD HANDLERS - Common logic implemented once
        // =========================================================================

        /// <summary>Handles save requests with auto-grow and path sanitization</summary>
        private async Task<object?> HandleSaveAsync(Dictionary<string, object> parameters)
        {
            var key = SanitizeKey((string)parameters["key"]);
            var data = ExtractData(parameters["data"]);

            // Auto-grow if needed
            await EnsureCapacityAsync(data.Length);

            // Delegate to plugin-specific write
            await WriteBytesAsync(key, data);

            // Update size tracking
            _currentSize += data.Length;

            return new { success = true, key, size = data.Length, storageType = StorageType };
        }

        /// <summary>Handles load requests</summary>
        private async Task<object?> HandleLoadAsync(Dictionary<string, object> parameters)
        {
            var key = SanitizeKey((string)parameters["key"]);
            var data = await ReadBytesAsync(key);
            return data;
        }

        /// <summary>Handles delete requests with auto-shrink</summary>
        private async Task<object?> HandleDeleteAsync(Dictionary<string, object> parameters)
        {
            var key = SanitizeKey((string)parameters["key"]);
            await DeleteBytesAsync(key);

            // Try to shrink if space freed up
            await TryShrinkAsync();

            return new { success = true, key, storageType = StorageType };
        }

        /// <summary>Handles existence checks</summary>
        private async Task<object?> HandleExistsAsync(Dictionary<string, object> parameters)
        {
            var key = SanitizeKey((string)parameters["key"]);
            var exists = await ExistsBytesAsync(key);
            return new { exists, key, storageType = StorageType };
        }

        /// <summary>Handles list requests</summary>
        private async Task<object?> HandleListAsync(Dictionary<string, object> parameters)
        {
            var prefix = parameters.ContainsKey("prefix") ? (string)parameters["prefix"] : "";
            var keys = await ListKeysAsync(prefix);
            return new { keys, count = keys.Count, storageType = StorageType };
        }

        // =========================================================================
        // HELPER METHODS - Cross-platform, filesystem-agnostic
        // =========================================================================

        /// <summary>
        /// Sanitizes key for cross-platform compatibility.
        /// Removes dangerous characters, normalizes paths.
        /// </summary>
        private string SanitizeKey(string key)
        {
            // Remove path traversal attempts
            key = key.Replace("..", "").Replace("//", "/");
            key = key.Trim('/', '\\');

            // Handle Windows vs Unix paths
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                key = key.Replace('/', '\\');
            else
                key = key.Replace('\\', '/');

            return key;
        }

        /// <summary>Extracts data from various formats (byte[]/Stream/base64)</summary>
        private byte[] ExtractData(object data)
        {
            return data switch
            {
                byte[] bytes => bytes,
                Stream stream => ReadStreamToBytes(stream),
                string base64 => Convert.FromBase64String(base64),
                _ => throw new ArgumentException($"Unsupported data type: {data.GetType().Name}")
            };
        }

        /// <summary>Reads stream to byte array</summary>
        private byte[] ReadStreamToBytes(Stream stream)
        {
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }

        /// <summary>Ensures storage capacity, grows if needed</summary>
        private async Task EnsureCapacityAsync(long additionalBytes)
        {
            if (_reservedSize == 0) return; // Unlimited

            if (_currentSize + additionalBytes > _reservedSize)
            {
                // Grow by 50% or required size, whichever is larger
                var growthNeeded = Math.Max(additionalBytes, _reservedSize / 2);
                _reservedSize += growthNeeded;
                Context?.LogInfo($"Storage grew to {_reservedSize / 1024 / 1024}MB");
            }
        }

        /// <summary>Tries to shrink storage if usage is low</summary>
        private async Task TryShrinkAsync()
        {
            if (_reservedSize == 0) return; // Unlimited

            // If using less than 60% of reserved space, shrink by 25%
            if (_currentSize < _reservedSize * 0.6)
            {
                _reservedSize = (long)(_reservedSize * 0.75);
                Context?.LogInfo($"Storage shrunk to {_reservedSize / 1024 / 1024}MB");
            }
        }

        /// <summary>Unmounts storage on shutdown</summary>
        protected override async Task OnShutdownAsync()
        {
            await UnmountInternalAsync();
        }
    }
}
