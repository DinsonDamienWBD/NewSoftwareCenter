using SoftwareCenter.Core.UI.Requests;
using System.Threading.Tasks;

namespace SoftwareCenter.Core.UI;

/// <summary>
/// Defines the contract for the UI Engine, which manages the entire application UI.
/// It handles requests for UI components like navigation buttons, content containers, and cards,
/// and manages their rendering and lifecycle.
/// </summary>
public interface IUIEngine
{
    /// <summary>
    /// Asynchronously requests the creation of a navigation button.
    /// </summary>
    /// <param name="request">The request object containing details for the navigation button.</param>
    /// <returns>A unique identifier for the created navigation button.</returns>
    Task<string> RequestNavButtonAsync(NavButtonRequest request);

    /// <summary>
    /// Asynchronously requests the creation of a content container associated with a navigation button.
    /// </summary>
    /// <param name="request">The request object containing details for the content container.</param>
    /// <returns>A unique identifier for the created content container.</returns>
    Task<string> RequestContentContainerAsync(ContentContainerRequest request);

    /// <summary>
    /// Asynchronously requests a dynamic update to an existing UI element.
    /// This can be used to add, remove, or modify controls within a container or card.
    /// </summary>
    /// <param name="request">The request object containing the details of the update.</param>
    /// <returns>A task that resolves to true if the update was successfully sent to the client, otherwise false.</returns>
    Task<bool> UpdateUIElementAsync(UIUpdateRequest request);

    /// <summary>
    /// Asynchronously removes a UI element (and its children) from the UI.
    /// </summary>
    /// <param name="elementId">The unique identifier of the element to remove.</param>
    /// <param name="ownerId">The ID of the owner requesting the removal, for validation.</param>
    /// <returns>A task that resolves to true if the removal was successfully sent to the client, otherwise false.</returns>
    Task<bool> RemoveUIElementAsync(string elementId, string ownerId);
}