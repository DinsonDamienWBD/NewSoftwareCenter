using System;
using System.Collections.Generic;
using SoftwareCenter.Core.Commands;

namespace SoftwareCenter.Core.Commands.UI
{
    /// <summary>
    /// A command to set or update one or more properties on an existing UI element.
    /// This is a "fire and forget" command that does not return a value.
    /// </summary>
    public class SetElementPropertiesCommand : ICommand
    {
        /// <summary>
        /// Gets the ID of the element to modify.
        /// </summary>
        public Guid ElementId { get; }

        /// <summary>
        /// Gets the dictionary of properties to set or update.
        /// </summary>
        public Dictionary<string, object> PropertiesToSet { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SetElementPropertiesCommand"/> class.
        /// </summary>
        /// <param name="elementId">The ID of the element to modify.</param>
        /// <param name="propertiesToSet">The properties to set on the element.</param>
        public SetElementPropertiesCommand(Guid elementId, Dictionary<string, object> propertiesToSet)
        {
            ElementId = elementId;
            PropertiesToSet = propertiesToSet ?? throw new ArgumentNullException(nameof(propertiesToSet));
        }
    }
}
