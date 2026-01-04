namespace DataWarehouse.SDK.Security
{
    /// <summary>
    /// Define the interface locally if not in Contracts yet (it was referenced in DataWarehouseWarehouse)
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

        /// <summary>
        /// Retrieves or creates an encryption key for the specified ID.
        /// Performs strict ACL checks against the provided security context.
        /// </summary>
        Task<byte[]> GetKeyAsync(string keyId, ISecurityContext context);

        /// <summary>
        /// Rotates or creates a new key.
        /// </summary>
        Task<byte[]> CreateKeyAsync(string keyId, ISecurityContext context);
    }
}
