using Core.Security;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace DataWarehouse.Security
{
    /// <summary>
    /// Implements AES-GCM (Galois/Counter Mode).
    /// Provides Authenticated Encryption (Confidentiality + Integrity).
    /// </summary>
    public class AesGcmAlgorithm : ICryptoAlgorithm
    {
        /// <summary>
        /// ID
        /// </summary>
        public string Id => "AES-GCM-256";
        private const int NonceSize = 12; // 96-bit nonce standard
        private const int TagSize = 16;   // 128-bit tag standard

        /// <summary>
        /// Encrypt
        /// </summary>
        /// <param name="data"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public byte[] Encrypt(byte[] data, byte[] key)
        {
            using var aes = new AesGcm(key, TagSize);

            var nonce = new byte[NonceSize];
            RandomNumberGenerator.Fill(nonce);

            var tag = new byte[TagSize];
            var ciphertext = new byte[data.Length];

            aes.Encrypt(nonce, data, ciphertext, tag);

            // Format: [Nonce 12b][Tag 16b][Ciphertext N]
            return nonce.Concat(tag).Concat(ciphertext).ToArray();
        }

        /// <summary>
        /// Decrypt
        /// </summary>
        /// <param name="blob"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public byte[] Decrypt(byte[] blob, byte[] key)
        {
            if (blob.Length < NonceSize + TagSize)
                throw new ArgumentException("Invalid encrypted blob size.");

            using var aes = new AesGcm(key, TagSize);

            var nonce = blob.AsSpan(0, NonceSize);
            var tag = blob.AsSpan(NonceSize, TagSize);
            var ciphertext = blob.AsSpan(NonceSize + TagSize);

            var plaintext = new byte[ciphertext.Length];
            aes.Decrypt(nonce, ciphertext, tag, plaintext);

            return plaintext;
        }
    }
}