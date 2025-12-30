using Core.Security;
using System.Security.Cryptography;
using System.Text;

namespace Manager.Security
{
    /// <summary>
    /// DPAPI-based Crypto Provider for encrypting and decrypting data using Windows Data Protection API.
    /// </summary>
    public class DpapiCryptoProvider : ICryptoProvider
    {
        // --- AES Implementation (Satisfies Interface) ---
        /// <summary>
        /// Encrypts the given output stream using AES encryption with the provided key and IV.
        /// </summary>
        /// <param name="outputStream"></param>
        /// <param name="key"></param>
        /// <param name="iv"></param>
        /// <returns></returns>
        public Stream Encrypt(Stream outputStream, byte[] key, byte[] iv)
        {
            var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            return new CryptoStream(outputStream, aes.CreateEncryptor(), CryptoStreamMode.Write);
        }

        /// <summary>
        /// Decrypts the given input stream using AES decryption with the provided key and IV.
        /// </summary>
        /// <param name="inputStream"></param>
        /// <param name="key"></param>
        /// <param name="iv"></param>
        /// <returns></returns>
        public Stream Decrypt(Stream inputStream, byte[] key, byte[] iv)
        {
            var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            return new CryptoStream(inputStream, aes.CreateDecryptor(), CryptoStreamMode.Read);
        }

        /// <summary>
        /// Generates a new AES key and IV.
        /// </summary>
        /// <returns></returns>
        public (byte[] Key, byte[] IV) GenerateKey()
        {
            using var aes = Aes.Create();
            aes.GenerateKey();
            aes.GenerateIV();
            return (aes.Key, aes.IV);
        }

        // --- DPAPI Implementation (Actual usage) ---
        /// <summary>
        /// Protects (encrypts) the given plain text using DPAPI.
        /// </summary>
        /// <param name="plainText"></param>
        /// <returns></returns>
        /// <exception cref="PlatformNotSupportedException"></exception>
        public string Protect(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return plainText;
            if (OperatingSystem.IsWindows())
            {
                byte[] data = Encoding.UTF8.GetBytes(plainText);
                byte[] encrypted = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(encrypted);
            }
            throw new PlatformNotSupportedException("DPAPI is only supported on Windows.");
        }

        /// <summary>
        /// Unprotects (decrypts) the given cipher text using DPAPI.
        /// </summary>
        /// <param name="cipherText"></param>
        /// <returns></returns>
        /// <exception cref="PlatformNotSupportedException"></exception>
        public string Unprotect(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return cipherText;
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    byte[] data = Convert.FromBase64String(cipherText);
                    byte[] decrypted = ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
                    return Encoding.UTF8.GetString(decrypted);
                }
                catch { return string.Empty; }
            }
            throw new PlatformNotSupportedException("DPAPI is only supported on Windows.");
        }
    }
}