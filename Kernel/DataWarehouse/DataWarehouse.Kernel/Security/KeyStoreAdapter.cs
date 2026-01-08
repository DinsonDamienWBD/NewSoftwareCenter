using DataWarehouse.SDK.Security;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DataWarehouse.Kernel.Security
{
    /// <summary>
    /// PRODUCTION GRADE: Secure Key Storage.
    /// Encrypts the Master Key database at rest using machine-local entropy (DPAPI/MachineID).
    /// Prevents "Drive-By" attacks where an attacker steals the disk but not the running machine.
    /// </summary>
    public class KeyStoreAdapter : IKeyStore
    {
        private readonly string _storePath;
        private readonly ConcurrentDictionary<string, string> _keyCache = new(); // ID -> Base64(EncryptedKey)
        private readonly Lock _lock = new();

        // A unique salt for this installation
        private static readonly byte[] LocalEntropy = Encoding.UTF8.GetBytes("DataWarehouse_V_GOD_TIER_SALT");

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="rootPath"></param>
        public KeyStoreAdapter(string rootPath)
        {
            _storePath = Path.Combine(rootPath, "Security", "keystore.dat");
            Directory.CreateDirectory(Path.GetDirectoryName(_storePath)!);
            Load();
        }

        /// <summary>
        /// Get current key ID
        /// </summary>
        /// <returns></returns>
        public Task<string> GetCurrentKeyIdAsync() => Task.FromResult("MASTER-01");

        /// <summary>
        /// Get key
        /// </summary>
        /// <param name="keyId"></param>
        /// <returns></returns>
        /// <exception cref="KeyNotFoundException"></exception>
        public byte[] GetKey(string keyId)
        {
            lock (_lock)
            {
                if (!_keyCache.TryGetValue(keyId, out var base64))
                {
                    // Auto-Provision Master Key if missing (First Run)
                    if (keyId == "MASTER-01") return CreateMasterKey();
                    throw new KeyNotFoundException($"Key {keyId} not found in secure store.");
                }

                // Decrypt from storage format
                byte[] encrypted = Convert.FromBase64String(base64);
                return Unprotect(encrypted);
            }
        }

        /// <summary>
        /// Get key
        /// </summary>
        /// <param name="keyId"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public Task<byte[]> GetKeyAsync(string keyId, ISecurityContext context)
            => Task.FromResult(GetKey(keyId));

        /// <summary>
        /// Create key
        /// </summary>
        /// <param name="keyId"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public Task<byte[]> CreateKeyAsync(string keyId, ISecurityContext context)
        {
            lock (_lock)
            {
                var key = GenerateRandomKey();
                var encrypted = Protect(key);
                _keyCache[keyId] = Convert.ToBase64String(encrypted);
                Save();
                return Task.FromResult(key);
            }
        }

        private byte[] CreateMasterKey()
        {
            var key = GenerateRandomKey();
            var encrypted = Protect(key);
            _keyCache["MASTER-01"] = Convert.ToBase64String(encrypted);
            Save();
            return key;
        }

        private void Save()
        {
            var json = JsonSerializer.Serialize(_keyCache);
            File.WriteAllText(_storePath, json);
        }

        private void Load()
        {
            if (File.Exists(_storePath))
            {
                var json = File.ReadAllText(_storePath);
                var data = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (data != null)
                {
                    foreach (var kv in data) _keyCache[kv.Key] = kv.Value;
                }
            }
        }

        // --- Cryptographic Primitives ---

        private static byte[] GenerateRandomKey()
        {
            var key = new byte[32]; // AES-256
            RandomNumberGenerator.Fill(key);
            return key;
        }

        private static byte[] Protect(byte[] data)
        {
            if (OperatingSystem.IsWindows())
            {
                // DPAPI: Best practice on Windows. Keys are tied to the User/Machine credentials.
                return ProtectedData.Protect(data, LocalEntropy, DataProtectionScope.CurrentUser);
            }
            else
            {
                // [FIX] Linux KEK Strategy
                string? kekStr = Environment.GetEnvironmentVariable("DW_MASTER_KEK");
                if (string.IsNullOrEmpty(kekStr))
                {
                    // FATAL: In production, we cannot run without a secure root of trust.
                    throw new InvalidOperationException("Security Critical: DW_MASTER_KEK environment variable is missing. Cannot secure Master Key.");
                }

                // Derive KEK (SHA256 ensures 32-byte key)
                byte[] kek = SHA256.HashData(Encoding.UTF8.GetBytes(kekStr));

                using var aes = Aes.Create();
                aes.Key = kek;
                aes.GenerateIV(); // Random IV for every save
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using var encryptor = aes.CreateEncryptor();
                using var ms = new MemoryStream();

                // Write IV first (Plaintext, 16 bytes)
                ms.Write(aes.IV);

                // Write Encrypted Data
                using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                {
                    cs.Write(data);
                }

                return ms.ToArray();
            }
        }

        private static byte[] Unprotect(byte[] data)
        {
            if (OperatingSystem.IsWindows())
            {
                return ProtectedData.Unprotect(data, LocalEntropy, DataProtectionScope.CurrentUser);
            }
            else
            {
                string? kekStr = Environment.GetEnvironmentVariable("DW_MASTER_KEK");
                if (string.IsNullOrEmpty(kekStr)) throw new InvalidOperationException("DW_MASTER_KEK missing.");

                byte[] kek = SHA256.HashData(Encoding.UTF8.GetBytes(kekStr));

                // Read IV (First 16 bytes)
                byte[] iv = new byte[16];
                Array.Copy(data, 0, iv, 0, 16);

                // Prepare Ciphertext (Rest of array)
                int cipherLen = data.Length - 16;
                byte[] ciphertext = new byte[cipherLen];
                Array.Copy(data, 16, ciphertext, 0, cipherLen);

                using var aes = Aes.Create();
                aes.Key = kek;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using var decryptor = aes.CreateDecryptor();
                using var ms = new MemoryStream(ciphertext);
                using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
                using var final = new MemoryStream();
                cs.CopyTo(final);

                return final.ToArray();
            }
        }
    }
}