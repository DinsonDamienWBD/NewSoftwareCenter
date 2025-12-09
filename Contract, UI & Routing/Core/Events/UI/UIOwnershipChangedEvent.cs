using SoftwareCenter.Core.UI;

namespace SoftwareCenter.Core.Events.UI
{
    /// <summary>
    /// Published when the ownership or permissions of a UI element have changed.
    /// </summary>
    public class UIOwnershipChangedEvent : IEvent
    {
        public string ElementId { get; }
        public UIAccessControl NewAccessControl { get; }

        public UIOwnershipChangedEvent(string elementId, UIAccessControl newAccessControl)
        {
            ElementId = elementId;
            NewAccessControl = newAccessControl;
        }
    }
}
