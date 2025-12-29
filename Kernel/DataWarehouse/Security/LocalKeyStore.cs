using System.Runtime.Versioning;
using System.Security.Cryptography;

namespace DataWarehouse.Security
{
    /// <summary>
    /// Manages the Master Encryption Key using Windows DPAPI.
    /// This ensures the key stored on disk cannot be read if copied to another machine.
    /// </summary>
    [SupportedOSPlatform("windows")]
    
    public class LocalKeyStore
    {
        private readonly string _keyPath;
        private readonly string _recoveryPath;
        private const int Iterations = 600_000; // OWASP recommendation for PBKDF2
        private const int SaltSize = 32;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="rootPath"></param>
        /// <exception cref="PlatformNotSupportedException"></exception>
        public LocalKeyStore(string rootPath)
        {
            // Fail fast if running on non-Windows OS with this specific driver
            if (!OperatingSystem.IsWindows())
            {
                throw new PlatformNotSupportedException("LocalKeyStore uses Windows DPAPI and cannot run on non-Windows operating systems.");
            }
            _keyPath = Path.Combine(rootPath, "master.key");
            _recoveryPath = Path.Combine(rootPath, "master.recovery");
        }

        /// <summary>
        /// Load existing key or generate a new key
        /// </summary>
        /// <returns></returns>
        public byte[] LoadOrGenerateKey()
        {
            if (File.Exists(_keyPath)) return LoadKey();
            if (File.Exists(_recoveryPath)) throw new UnauthorizedAccessException("Primary key missing. Use UnlockWithPassword().");

            return GenerateKey();
        }

        /// <summary>
        /// If DPAPI cannot unlock, fallback to password
        /// </summary>
        /// <param name="password"></param>
        /// <returns></returns>
        /// <exception cref="FileNotFoundException"></exception>
        public byte[] UnlockWithPassword(string password)
        {
            if (!File.Exists(_recoveryPath)) throw new FileNotFoundException("No recovery key found.");

            var fileBytes = File.ReadAllBytes(_recoveryPath);
            var salt = fileBytes.AsSpan(0, SaltSize).ToArray(); // Copy to array for cleanup
            byte[]? kek = null;

            try
            {
                var nonce = fileBytes.AsSpan(SaltSize, 12);
                var tag = fileBytes.AsSpan(SaltSize + 12, 16);
                var ciphertext = fileBytes.AsSpan(SaltSize + 12 + 16);

                kek = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);

                using var aes = new AesGcm(kek, 16);
                var masterKey = new byte[ciphertext.Length];
                aes.Decrypt(nonce, ciphertext, tag, masterKey);

                SaveDpapiKey(masterKey);
                return masterKey;
            }
            finally
            {
                // HYGIENE: Wipe intermediate secrets
                CryptographicOperations.ZeroMemory(salt);
                if (kek != null) CryptographicOperations.ZeroMemory(kek);
            }
        }

        /// <summary>
        /// Allow to set a recovery password
        /// </summary>
        /// <param name="masterKey"></param>
        /// <param name="password"></param>
        public void SetRecoveryPassword(byte[] masterKey, string password)
        {
            var salt = new byte[SaltSize];
            RandomNumberGenerator.Fill(salt);
            byte[]? kek = null;

            try
            {
                kek = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);

                using var aes = new AesGcm(kek, 16);
                var nonce = new byte[12];
                RandomNumberGenerator.Fill(nonce);

                var ciphertext = new byte[masterKey.Length];
                var tag = new byte[16];

                aes.Encrypt(nonce, masterKey, ciphertext, tag);

                using var fs = new FileStream(_recoveryPath, FileMode.Create);
                fs.Write(salt);
                fs.Write(nonce);
                fs.Write(tag);
                fs.Write(ciphertext);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(salt);
                if (kek != null) CryptographicOperations.ZeroMemory(kek);
            }
        }

        private byte[] GenerateKey()
        {
            var key = new byte[32];
            RandomNumberGenerator.Fill(key);
            SaveDpapiKey(key);
            return key;
        }

        private void SaveDpapiKey(byte[] key)
        {
            var encryptedKey = ProtectedData.Protect(key, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(_keyPath, encryptedKey);
        }

        private byte[] LoadKey()
        {
            try
            {
                var encrypted = File.ReadAllBytes(_keyPath);
                return ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            }
            catch (CryptographicException)
            {
                throw new UnauthorizedAccessException("DPAPI Decryption failed. Key may be from another machine.");
            }
        }
    }
}