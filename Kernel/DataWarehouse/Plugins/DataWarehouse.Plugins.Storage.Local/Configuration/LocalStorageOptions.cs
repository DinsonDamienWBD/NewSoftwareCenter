namespace DataWarehouse.Plugins.Storage.LocalFileSystem.Configuration
{
    /// <summary>
    /// Configuration options for the Local File System Plugin.
    /// </summary>
    public class LocalStorageOptions
    {
        /// <summary>
        /// Defines how data is physically stored on the disk.
        /// </summary>
        public StorageMode Mode { get; set; } = StorageMode.Folder;

        /// <summary>
        /// The block size for VDI mode (Default: 4096 bytes).
        /// </summary>
        public int VdiBlockSize { get; set; } = 4096;

        /// <summary>
        /// The initial size of the VDI container in bytes (Default: 100MB).
        /// It will grow automatically.
        /// </summary>
        public long VdiInitialSize { get; set; } = 100 * 1024 * 1024;
    }

    /// <summary>
    /// The operational mode of the storage engine.
    /// </summary>
    public enum StorageMode
    {
        /// <summary>
        /// Stores blobs as individual files in the OS file system.
        /// Best for: Debugging, Transparency, Single User.
        /// </summary>
        Folder,

        /// <summary>
        /// Stores blobs inside a single Virtual Disk Image (.vdi) file.
        /// Best for: Performance, Portable Deployment, Massive File Counts.
        /// </summary>
        Vdi
    }
}