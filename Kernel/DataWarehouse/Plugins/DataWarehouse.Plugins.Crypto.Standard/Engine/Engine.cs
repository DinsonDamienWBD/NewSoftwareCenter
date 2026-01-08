using System.Security.Cryptography;

namespace DataWarehouse.Plugins.Crypto.Standard.Engine
{
    /// <summary>
    /// Core AES encryption/decryption engine.
    /// Implements AES-256-CBC with automatic IV generation and prepending.
    /// </summary>
    internal static class AESEngine
    {
        private const int KeySize = 256; // AES-256
        private const int BlockSize = 128; // 128-bit blocks

        /// <summary>Encrypts data using AES-256-CBC</summary>
        public static async Task<byte[]> EncryptAsync(byte[] input, Dictionary<string, object> args)
        {
            // Get or generate key
            var key = GetKey(args);

            using var aes = Aes.Create();
            aes.KeySize = KeySize;
            aes.BlockSize = BlockSize;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = key;
            aes.GenerateIV(); // Generate random IV

            using var encryptor = aes.CreateEncryptor();
            using var outputStream = new MemoryStream();

            // Prepend IV to output
            await outputStream.WriteAsync(aes.IV, 0, aes.IV.Length);

            // Encrypt data
            using (var cryptoStream = new CryptoStream(outputStream, encryptor, CryptoStreamMode.Write))
            {
                await cryptoStream.WriteAsync(input, 0, input.Length);
            }

            return outputStream.ToArray();
        }

        /// <summary>Decrypts AES-256-CBC encrypted data</summary>
        public static async Task<byte[]> DecryptAsync(byte[] input, Dictionary<string, object> args)
        {
            // Get key
            var key = GetKey(args);

            using var aes = Aes.Create();
            aes.KeySize = KeySize;
            aes.BlockSize = BlockSize;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = key;

            // Extract IV from input (first 16 bytes)
            var iv = new byte[16];
            Array.Copy(input, 0, iv, 0, 16);
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            using var inputStream = new MemoryStream(input, 16, input.Length - 16);
            using var cryptoStream = new CryptoStream(inputStream, decryptor, CryptoStreamMode.Read);
            using var outputStream = new MemoryStream();

            await cryptoStream.CopyToAsync(outputStream);
            return outputStream.ToArray();
        }

        /// <summary>Gets encryption key from args or generates default</summary>
        private static byte[] GetKey(Dictionary<string, object> args)
        {
            if (args.ContainsKey("key"))
            {
                if (args["key"] is byte[] keyBytes)
                    return keyBytes;
                if (args["key"] is string keyStr)
                    return Convert.FromBase64String(keyStr);
            }

            // TODO: Get from key management system
            // For now, use deterministic key (NOT SECURE - placeholder only)
            using var sha = SHA256.Create();
            return sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes("DataWarehouse-Default-Key"));
        }
    }
}
