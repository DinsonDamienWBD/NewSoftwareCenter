using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using SoftwareCenter.Core.UI;
using SoftwareCenter.Core.UI.Client;
using SoftwareCenter.Core.UI.Requests;
using SoftwareCenter.UI.Engine.Services;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace SoftwareCenter.UI.Engine;

public class UIEngine : IUIEngine
{
    private readonly IHubContext<UIHub> _hubContext;
    private readonly ILogger<UIEngine> _logger;
    private readonly ConcurrentDictionary<string, UIElement> _uiState = new();

    public UIEngine(IHubContext<UIHub> hubContext, ILogger<UIEngine> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task<string> RequestNavButtonAsync(NavButtonRequest request)
    {
        var button = new ClientNavButton
        {
            Id = $"nav-{Guid.NewGuid()}",
            Label = request.Label,
            Icon = request.Icon,
            Priority = request.Priority
        };

        _uiState.TryAdd(button.Id, new UIElement(button.Id, request.OwnerId, UIElementType.NavButton, button));

        await _hubContext.Clients.All.SendAsync("RenderNavButton", button);

        return button.Id;
    }

    public async Task<string> RequestContentContainerAsync(ContentContainerRequest request)
    {
        // Ensure the associated nav button exists and is owned by the requestor
        if (!_uiState.TryGetValue(request.AssociatedNavButtonId, out var navButton) || navButton.OwnerId != request.OwnerId)
        {
            _logger.LogError("Request for content container from owner {OwnerId} failed. Associated nav button {NavButtonId} does not exist or is not owned by the requester.", request.OwnerId, request.AssociatedNavButtonId);
            return string.Empty; // Return empty string to indicate failure without crashing the caller.
        }

        var container = new ClientContentContainer
        {
            Id = $"container-{Guid.NewGuid()}",
            OwnerId = request.OwnerId,
            AssociatedNavButtonId = request.AssociatedNavButtonId,
            HasSpa = !string.IsNullOrWhiteSpace(request.SpaUrl),
            SpaUrl = request.SpaUrl
        };

        _uiState.TryAdd(container.Id, new UIElement(container.Id, request.OwnerId, UIElementType.Container, container));

        await _hubContext.Clients.All.SendAsync("RenderContentContainer", container);

        return container.Id;
    }

    public async Task<bool> UpdateUIElementAsync(UIUpdateRequest request)
    {
        // Ensure the element exists and is owned by the requestor
        if (!_uiState.TryGetValue(request.TargetElementId, out var element) || element.OwnerId != request.OwnerId)
        {
            _logger.LogWarning("Unauthorized attempt to update UI element {ElementId} by owner {OwnerId}.", request.TargetElementId, request.OwnerId);
            return false;
        }

        await _hubContext.Clients.All.SendAsync("UpdateElementContent", request.TargetElementId, request.CustomHtmlContent ?? string.Empty);
        return true;
    }

    public async Task<bool> RemoveUIElementAsync(string elementId, string ownerId)
    {
        // Ensure the element exists and is owned by the requestor
        if (!_uiState.TryGetValue(elementId, out var element) || element.OwnerId != ownerId)
        {
            _logger.LogWarning("Unauthorized attempt to remove UI element {ElementId} by owner {OwnerId}.", elementId, ownerId);
            return false;
        }

        if (_uiState.TryRemove(elementId, out _))
        {
            await _hubContext.Clients.All.SendAsync("RemoveElement", elementId);

            // Also remove any associated containers if a nav button is removed
            if (element.Type == UIElementType.NavButton)
            {
                var associatedContainers = _uiState.Where(kvp =>
                        kvp.Value.Type == UIElementType.Container &&
                        kvp.Value.StateObject is ClientContentContainer container &&
                        container.AssociatedNavButtonId == elementId)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var containerId in associatedContainers)
                {
                    if (_uiState.TryRemove(containerId, out _))
                    {
                        await _hubContext.Clients.All.SendAsync("RemoveElement", containerId);
                    }
                }
            }
        }
        return true;
    }
}