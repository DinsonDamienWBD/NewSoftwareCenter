using System.Collections.Generic;

namespace SoftwareCenter.Core.Commands.UI
{
    /// <summary>
    /// A command to create a new UI element in the UI Manager.
    /// </summary>
    public class CreateUIElementCommand : ICommand<string>
    {
        /// <summary>
        /// Gets the ID of the module that will own this new element.
        /// </summary>
        public string OwnerModuleId { get; }

        /// <summary>
        /// Gets the ID of the parent element to which this new element should be attached.
        /// </summary>
        public string ParentId { get; }

        /// <summary>
        /// Gets the type of the element to create (e.g., "Panel", "Button").
        /// </summary>
        public string ElementType { get; }

        /// <summary>
        /// Gets a dictionary of initial properties to set on the new element.
        /// </summary>
        public Dictionary<string, object> InitialProperties { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CreateUIElementCommand"/> class.
        /// </summary>
        /// <param name="ownerModuleId">The ID of the module that will own this new element.</param>
        /// <param name="parentId">The ID of the parent element.</param>
        /// <param name="elementType">The type of element to create.</param>
        /// <param name="initialProperties">Optional initial properties for the element.</param>
        public CreateUIElementCommand(string ownerModuleId, string parentId, string elementType, Dictionary<string, object> initialProperties = null)
        {
            OwnerModuleId = ownerModuleId;
            ParentId = parentId;
            ElementType = elementType;
            InitialProperties = initialProperties ?? new Dictionary<string, object>();
        }
    }
}
