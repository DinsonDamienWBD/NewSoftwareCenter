using DataWarehouse.Plugins.Crypto.Standard.Engine;
using DataWarehouse.SDK.Contracts;
using System.Security.Cryptography;

namespace DataWarehouse.Plugins.Crypto.Standard.Bootstrapper
{
    /// <summary>
    /// Standard AES encryption plugin
    /// </summary>
    public class StandardAesPlugin : IFeaturePlugin, IDataTransformation
    {
        /// <summary>
        /// ID
        /// </summary>
        public string Id => "standard-aes";

        /// <summary>
        /// Name
        /// </summary>
        public string Name => "Standard AES";

        /// <summary>
        /// Version
        /// </summary>
        public string Version => "1.1";

        /// <summary>
        /// Security level
        /// </summary>
        public SecurityLevel Level => SecurityLevel.Standard;

        /// <summary>
        /// Block size
        /// </summary>
        public int BlockSize => 16; // AES block size

        public string Category => "Encryption";
        public int QualityLevel => 50; // Balanced

        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="context"></param>
        public void Initialize(IKernelContext context) { }

        public Task StartAsync(CancellationToken c) => Task.CompletedTask;
        public Task StopAsync() => Task.CompletedTask;

        /// <summary>
        /// Create encryptor
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public ICryptoTransform CreateEncryptor(byte[] key)
        {
            using var aes = Aes.Create();
            aes.Key = key;
            return aes.CreateEncryptor();
        }

        /// <summary>
        /// Create decryptor
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public ICryptoTransform CreateDecryptor(byte[] key)
        {
            using var aes = Aes.Create();
            aes.Key = key;
            return aes.CreateDecryptor();
        }

        public Stream OnWrite(Stream input, IKernelContext context, Dictionary<string, object> args)
        {
            var key = (byte[])args["Key"]; // Get from Kernel

            // 1. Generate Random IV
            byte[] iv = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(iv);
            }

            // 2. Create Encryptor
            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            // 3. Create CryptoStream (Outputs Ciphertext)
            // Note: We use 'Read' mode because the Kernel reads *from* this stream to save to disk.
            // This CryptoStream wraps the 'input' (Plaintext Source).
            var cryptoStream = new CryptoStream(input, aes.CreateEncryptor(), CryptoStreamMode.Read);

            // 4. Prepend IV to the output so we can decrypt later
            return new IvPrependStream(cryptoStream, iv);
        }

        public Stream OnRead(Stream stored, IKernelContext context, Dictionary<string, object> args)
        {
            var key = (byte[])args["Key"];

            // 1. Read IV from the head of the stream
            byte[] iv = new byte[16];
            int bytesRead = stored.Read(iv, 0, 16);

            if (bytesRead < 16)
            {
                // Stream is too short to even contain an IV
                throw new InvalidDataException("Stream is corrupted or too short to contain IV.");
            }

            // 2. Create Decryptor using the extracted IV
            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            // 3. Return Decrypting Stream
            // 'stored' is now positioned at byte 16 (start of ciphertext), which is exactly what we want.
            return new CryptoStream(stored, aes.CreateDecryptor(), CryptoStreamMode.Read);
        }
    }
}