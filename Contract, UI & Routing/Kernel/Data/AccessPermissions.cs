using System;

namespace SoftwareCenter.Kernel.Data
{
    /// <summary>
    /// Defines the types of access permissions that can be granted to other modules
    /// for a data item in the Global Data Store.
    /// </summary>
    [Flags]
    public enum AccessPermissions
    {
        /// <summary>
        /// No specific permissions granted.
        /// </summary>
        None = 0,

        /// <summary>
        /// Allows reading the data item.
        /// </summary>
        Read = 1 << 0,

        /// <summary>
        /// Allows updating the data item.
        /// </summary>
        Write = 1 << 1,

        /// <summary>
        /// Allows deleting the data item.
        /// </summary>
        Delete = 1 << 2,

        /// <summary>
        /// Allows sharing the data item with other modules.
        /// </summary>
        Share = 1 << 3,

        /// <summary>
        /// Allows transferring ownership of the data item to another module.
        /// </summary>
        TransferOwnership = 1 << 4,

        /// <summary>
        /// Grants all available permissions.
        /// </summary>
        All = Read | Write | Delete | Share | TransferOwnership
    }
}
