using System.Collections.Generic;

namespace SoftwareCenter.Core.UI
{
    /// <summary>
    /// Defines the ownership and access permissions for a UIElement.
    /// </summary>
    public class UIAccessControl
    {
        /// <summary>
        /// The unique identifier of the module that owns the element.
        /// </summary>
        public string OwnerId { get; set; }

        /// <summary>
        /// A dictionary mapping Module IDs to their specific permissions on the element.
        /// This allows for sharing control with other modules.
        /// (Permissions model to be defined later, e.g., as an enum: Read, Write, FullControl)
        /// </summary>
        public Dictionary<string, SoftwareCenter.Core.Data.AccessPermissions> SharedAccess { get; set; } = new Dictionary<string, SoftwareCenter.Core.Data.AccessPermissions>();
    }
}
