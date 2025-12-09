using Microsoft.AspNetCore.SignalR;
using SoftwareCenter.Core.Events;
using SoftwareCenter.Core.Events.UI;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SoftwareCenter.Host.Services
{
    /// <summary>
    /// Listens for UI-related events from the core event bus and notifies
    /// connected SignalR clients of the changes. This class acts as the
    /// bridge between the .NET backend and the JavaScript frontend.
    /// </summary>
    public class UIHubNotifier :
        IEventHandler<UIElementRegisteredEvent>,
        IEventHandler<UIElementUnregisteredEvent>,
        IEventHandler<UIElementUpdatedEvent>
    {
        private readonly IHubContext<UIHub> _hubContext;

        public UIHubNotifier(IHubContext<UIHub> hubContext)
        {
            _hubContext = hubContext;
        }

        /// <summary>
        /// Handles the event for a new UI element being registered.
        /// </summary>
        public Task Handle(UIElementRegisteredEvent anEvent)
        {
            // The client-side app expects a specific structure.
            var payload = new
            {
                elementId = anEvent.NewElement.Id,
                parentId = anEvent.NewElement.ParentId,
                htmlContent = anEvent.HtmlContent,
                cssContent = anEvent.CssContent,
                jsContent = anEvent.JsContent
            };
            return _hubContext.Clients.All.SendAsync("ElementAdded", payload);
        }

        /// <summary>
        /// Handles the event for a UI element being removed.
        /// </summary>
        public Task Handle(UIElementUnregisteredEvent anEvent)
        {
            var payload = new { elementId = anEvent.ElementId };
            return _hubContext.Clients.All.SendAsync("ElementRemoved", payload);
        }

        /// <summary>
        /// Handles the event for a UI element being updated.
        /// </summary>
        public Task Handle(UIElementUpdatedEvent anEvent)
        {
            var payload = new
            {
                elementId = anEvent.ElementId,
                attributes = anEvent.UpdatedProperties
            };
            return _hubContext.Clients.All.SendAsync("ElementUpdated", payload);
        }
    }
}
