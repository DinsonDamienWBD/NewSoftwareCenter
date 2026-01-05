namespace DataWarehouse.SDK.Contracts
{
    /// <summary>
    /// Defines the operational environment of the Data Warehouse.
    /// Used by the Kernel to automatically select the best plugins (Intelligent Defaults).
    /// </summary>
    public enum OperatingMode
    {
        /// <summary>
        /// Resource-constrained environment (e.g., Battery powered, Low RAM).
        /// Prefers: In-Memory Index, Compression (Save Disk), Single Threading.
        /// </summary>
        Laptop,

        /// <summary>
        /// Standard workstation (e.g., Desktop PC).
        /// Prefers: SQLite Index, Folder Storage, Balanced Threading.
        /// </summary>
        Workstation,

        /// <summary>
        /// High-performance environment (e.g., Dedicated Server).
        /// Prefers: Postgres Index, VDI Storage, High Concurrency, Background Optimization.
        /// </summary>
        Server,

        /// <summary>
        /// Containerized or Cloud Cluster (e.g., Docker, Kubernetes).
        /// Prefers: Network Storage, Raft Consensus, Stateless Operation.
        /// </summary>
        Hyperscale
    }
}