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
        private readonly string _cachedKeyId = "MASTER-01";
        private byte[]? _cachedKey;

        /// <summary>
        /// Get current key ID
        /// </summary>
        /// <returns></returns>
        public Task<string> GetCurrentKeyIdAsync() => Task.FromResult(_cachedKeyId);

        /// <summary>
        /// Get key
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

        private byte[] LoadOrGenerateKey()
        {
            // 1. Priority: Environment Variable (Container/Cloud Friendly)
            var envKey = Environment.GetEnvironmentVariable("COSMIC_MASTER_KEY");
            if (!string.IsNullOrWhiteSpace(envKey))
            {
                try { return Convert.FromBase64String(envKey); }
                catch { throw new InvalidOperationException("Invalid Base64 in COSMIC_MASTER_KEY"); }
            }

            // 2. Priority: Existing File
            if (File.Exists(_keyPath)) return LoadKeyFromFile();

            // 3. Generate New
            return GenerateKey();
        }

        private byte[] GenerateKey()
        {
            var key = new byte[32]; // AES-256
            RandomNumberGenerator.Fill(key);

            if (OperatingSystem.IsWindows())
            {
                var encrypted = ProtectedData.Protect(key, null, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(_keyPath, encrypted);
                return key;
            }

            // LINUX/DOCKER SAFEGUARD:
            // We DO NOT write plaintext keys to disk in production.
            // If user didn't provide ENV var, and we can't use DPAPI, we must not persist insecurely.
            // For "Plug & Play" convenience in dev, we can warn, but for "God Tier", we strictly enforce security.

            throw new PlatformNotSupportedException(
                "On Linux/Mac, you MUST set the 'COSMIC_MASTER_KEY' environment variable. " +
                "Writing plaintext keys to disk is strictly forbidden in this edition.");
        }

        private byte[] LoadKeyFromFile()
        {
            var bytes = File.ReadAllBytes(_keyPath);
            if (OperatingSystem.IsWindows())
            {
                try { return ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser); }
                catch { throw new UnauthorizedAccessException("DPAPI Decryption failed. Did the user change?"); }
            }

            // If file exists on Linux, it implies a previous insecure version or manual setup. 
            // We reject loading it to prevent using compromised keys.
            throw new PlatformNotSupportedException("Key file found on non-Windows system. Use COSMIC_MASTER_KEY.");
        }
    }
}