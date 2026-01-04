namespace DataWarehouse.SDK.Contracts
{
    /// <summary>
    /// Represents the performance characteristic of a storage node.
    /// </summary>
    public enum StorageTier 
    { 
        /// <summary>
        /// Hot
        /// </summary>
        Hot, 
        
        /// <summary>
        /// Warm
        /// </summary>
        Warm, 
        
        /// <summary>
        /// Cold
        /// </summary>
        Cold 
    }
}
