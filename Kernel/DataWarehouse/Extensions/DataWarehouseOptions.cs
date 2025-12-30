namespace DataWarehouse.Extensions
{
    /// <summary>
    /// How to store the index
    /// </summary>
    public enum IndexStorageType
    {
        /// <summary>
        /// Let the Warehouse decide (Smart Mode)
        /// </summary>
        Auto,

        /// <summary>
        /// Force In-Memory (Fast, lost on restart)
        /// </summary>
        Volatile,

        /// <summary>
        /// Force SQLite (Safe, survives restart)
        /// </summary>
        Persistent
    }

    /// <summary>
    /// The DW options
    /// </summary>
    public class DataWarehouseOptions
    {
        /// <summary>
        /// Root path
        /// </summary>
        public string RootPath { get; set; } = string.Empty;

        /// <summary>
        /// Index storage type
        /// </summary>
        public IndexStorageType IndexType { get; set; } = IndexStorageType.Auto;
    }
}