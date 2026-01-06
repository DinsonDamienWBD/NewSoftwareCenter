using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DataWarehouse.Plugins.Crypto.Standard.Engine;
using DataWarehouse.SDK.Contracts;
using DataWarehouse.SDK.Contracts.CategoryBases;

namespace DataWarehouse.Plugins.Crypto.Standard.Bootstrapper
{
    /// <summary>
    /// AES encryption plugin using the standardized pipeline base.
    /// Provides AES-256-CBC encryption/decryption with automatic IV handling.
    /// </summary>
    public class AESEncryptionPlugin : PipelinePluginBase
    {
        /// <summary>Constructs the AES encryption plugin</summary>
        public AESEncryptionPlugin()
            : base(
                id: "DataWarehouse.Pipeline.AES",
                name: "AES-256 Encryption",
                version: new Version(2, 0, 0))
        {
        }

        /// <summary>Transformation type for AES</summary>
        protected override string TransformType => "aes";

        /// <summary>Encrypts data using AES-256-CBC</summary>
        protected override async Task<byte[]> ApplyTransformAsync(byte[] input, Dictionary<string, object> args)
        {
            return await AESEngine.EncryptAsync(input, args);
        }

        /// <summary>Decrypts AES-256-CBC encrypted data</summary>
        protected override async Task<byte[]> ReverseTransformAsync(byte[] input, Dictionary<string, object> args)
        {
            return await AESEngine.DecryptAsync(input, args);
        }

        /// <summary>Custom initialization for AES</summary>
        protected override void InitializePipeline(IKernelContext context)
        {
            context.LogInfo("AES-256-CBC encryption plugin initialized");
        }
    }
}
