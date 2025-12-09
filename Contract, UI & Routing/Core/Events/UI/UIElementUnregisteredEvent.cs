namespace SoftwareCenter.Core.Events.UI
{
    /// <summary>
    /// Published when a UI element is unregistered and removed from the UIManager.
    /// </summary>
    public class UIElementUnregisteredEvent : IEvent
    {
        public string ElementId { get; }

        public UIElementUnregisteredEvent(string elementId)
        {
            ElementId = elementId;
        }
    }
}
