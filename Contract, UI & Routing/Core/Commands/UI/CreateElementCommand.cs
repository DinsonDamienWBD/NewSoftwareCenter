using System;
using System.Collections.Generic;
using SoftwareCenter.Core.Commands;
using SoftwareCenter.Core.UI;

namespace SoftwareCenter.Core.Commands.UI
{
    /// <summary>
    /// A command to create a new UI element.
    /// Returns the unique ID of the newly created element.
    /// </summary>
    public class CreateElementCommand : ICommand<Guid>
    {
        /// <summary>
        /// Gets the type of the element to create.
        /// </summary>
        public ElementType ElementType { get; }

        /// <summary>
        /// Gets the ID of the parent element to attach this new element to.
        /// If null, it may be attached to a default root container by the UIManager.
        /// </summary>
        public Guid? ParentId { get; }

        /// <summary>
        /// Gets an optional set of initial properties for the element.
        /// </summary>
        public Dictionary<string, object> InitialProperties { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CreateElementCommand"/> class.
        /// </summary>
        /// <param name="elementType">The type of element to create.</param>
        /// <param name="parentId">The ID of the parent element.</param>
        /// <param name="initialProperties">A dictionary of initial properties.</param>
        public CreateElementCommand(ElementType elementType, Guid? parentId = null, Dictionary<string, object> initialProperties = null)
        {
            ElementType = elementType;
            ParentId = parentId;
            InitialProperties = initialProperties ?? new Dictionary<string, object>();
        }
    }
}
