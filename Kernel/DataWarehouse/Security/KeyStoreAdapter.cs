using System.Security.Cryptography;

namespace DataWarehouse.Security
{
    /// <summary>
    /// Define the interface locally if not in Contracts yet (it was referenced in CosmicWarehouse)
    /// </summary>
    public interface IKeyStore
    {
        /// <summary>
        /// Get the current key ID
        /// </summary>
        /// <returns></returns>
        Task<string> GetCurrentKeyIdAsync();

        /// <summary>
        /// Get the key
        /// </summary>
        /// <param name="keyId"></param>
        /// <returns></returns>
        byte[] GetKey(string keyId);
    }

    /// <summary>
    /// Adapter to use IKeyStore
    /// </summary>
    public class KeyStoreAdapter(string rootPath) : IKeyStore
    {
        private readonly string _keyPath = Path.Combine(rootPath, "master.key");
        private readonly string _recoveryPath = Path.Combine(rootPath, "master.recovery");
        private readonly string _cachedKeyId = "MASTER-01";
        private byte[]? _cachedKey;

        /// <summary>
        /// Get current key ID
        /// </summary>
        /// <returns></returns>
        public Task<string> GetCurrentKeyIdAsync() => Task.FromResult(_cachedKeyId);

        /// <summary>
        /// Get the key
        /// </summary>
        /// <param name="keyId"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public byte[] GetKey(string keyId)
        {
            if (keyId != _cachedKeyId) throw new ArgumentException("Unknown Key ID");
            _cachedKey ??= LoadOrGenerateKey();
            return _cachedKey;
        }

        // --- DPAPI Logic (Inlined from deleted LocalKeyStore) ---

        private byte[] LoadOrGenerateKey()
        {
            if (File.Exists(_keyPath)) return LoadKey();
            return GenerateKey();
        }

        private byte[] GenerateKey()
        {
            var key = new byte[32];
            RandomNumberGenerator.Fill(key);

            // Encrypt with DPAPI (User Scope)
            if (OperatingSystem.IsWindows())
            {
                var encrypted = ProtectedData.Protect(key, null, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(_keyPath, encrypted);
            }
            else
            {
                // Fallback for non-Windows (or throw PlatformNotSupported)
                File.WriteAllBytes(_keyPath, key);
            }
            return key;
        }

        private byte[] LoadKey()
        {
            var bytes = File.ReadAllBytes(_keyPath);
            if (OperatingSystem.IsWindows())
            {
                try { return ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser); }
                catch { throw new UnauthorizedAccessException("DPAPI Decryption failed."); }
            }
            return bytes;
        }
    }
}