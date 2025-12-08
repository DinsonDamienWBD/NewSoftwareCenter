using System;
using System.Collections.Generic;

namespace SoftwareCenter.Core.UI
{
    /// <summary>
    /// Represents a generic UI element within the application.
    /// This is a data contract managed by the UIManager and rendered by the Host.
    /// </summary>
    public class UIElement
    {
        /// <summary>
        /// Gets the unique identifier for this UI element.
        /// </summary>
        public Guid Id { get; }

        /// <summary>
        /// Gets the ID of the module that owns this element.
        /// </summary>
        public string OwnerModuleId { get; }

        /// <summary>
        /// Gets the ID of the parent element, if any.
        /// </summary>
        public Guid? ParentId { get; set; }

        /// <summary>
        /// Gets the type of the element.
        /// </summary>
        public ElementType Type { get; }

        /// <summary>
        /// Gets a dictionary of properties that define the element's appearance and behavior.
        /// Keys are property names (e.g., "Text", "Width", "BackgroundColor").
        /// </summary>
        public Dictionary<string, object> Properties { get; } = new Dictionary<string, object>();

        /// <summary>
        /// Initializes a new instance of the <see cref="UIElement"/> class.
        /// </summary>
        /// <param name="id">The unique ID for the element.</param>
        /// <param name="type">The type of the element.</param>
        /// <param name="ownerModuleId">The ID of the module creating the element.</param>
        public UIElement(Guid id, ElementType type, string ownerModuleId)
        {
            Id = id;
            Type = type;
            OwnerModuleId = ownerModuleId;
        }
    }
}
