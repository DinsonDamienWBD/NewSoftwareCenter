namespace Core.Security
{
    /// <summary>
    /// Plugin contract for Data Warehouse encryption strategies.
    /// Allows the DW to support multiple algorithms (AES, ChaCha20, etc.) simultaneously.
    /// </summary>
    public interface ICryptoAlgorithm
    {
        /// <summary>
        /// Unique Identifier for the algorithm (e.g., "AES-256-GCM").
        /// This ID is stored in the file header to enable dynamic decryption.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Encrypts data using the specified key.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        byte[] Encrypt(byte[] data, byte[] key);

        /// <summary>
        /// Decrypts data using the specified key.
        /// </summary>
        /// <param name="blob"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        byte[] Decrypt(byte[] blob, byte[] key);
    }
}