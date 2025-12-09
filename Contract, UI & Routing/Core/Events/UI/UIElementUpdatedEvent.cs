using System.Collections.Generic;

namespace SoftwareCenter.Core.Events.UI
{
    /// <summary>
    /// Published when one or more properties of a UI element have been updated.
    /// </summary>
    public class UIElementUpdatedEvent : IEvent
    {
        public string ElementId { get; }
        
        /// <summary>
        /// A dictionary containing only the properties that have changed.
        /// </summary>
        public Dictionary<string, object> UpdatedProperties { get; }

        public UIElementUpdatedEvent(string elementId, Dictionary<string, object> updatedProperties)
        {
            ElementId = elementId;
            UpdatedProperties = updatedProperties;
        }
    }
}
