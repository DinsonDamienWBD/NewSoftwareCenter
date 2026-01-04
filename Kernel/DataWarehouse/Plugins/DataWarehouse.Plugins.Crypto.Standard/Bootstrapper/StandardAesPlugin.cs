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
        public string Version => "1.0";

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
            var aes = Aes.Create();
            aes.Key = key;
            // Create Encryptor Stream
            return new CryptoStream(input, aes.CreateEncryptor(key, new byte[16]), CryptoStreamMode.Read);
        }

        public Stream OnRead(Stream stored, IKernelContext context, Dictionary<string, object> args)
        {
            var key = (byte[])args["Key"];
            var aes = Aes.Create();
            aes.Key = key;
            // Create Decryptor Stream
            return new CryptoStream(stored, aes.CreateDecryptor(key, new byte[16]), CryptoStreamMode.Read);
        }
    }
}