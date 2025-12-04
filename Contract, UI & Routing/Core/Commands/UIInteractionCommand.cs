using SoftwareCenter.Core.Diagnostics;
using System;
using System.Collections.Generic;

namespace SoftwareCenter.Core.Commands;

/// <summary>
/// Represents a command initiated by a user interaction with a UI element owned by a module.
/// </summary>
public class UIInteractionCommand : ICommand
{
    /// <summary>
    /// The name of the command, formatted to be routed to the correct module.
    /// Format: "{OwnerId}.UI.{Action}"
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The unique identifier of the module that owns the UI element.
    /// </summary>
    public string OwnerId { get; }

    /// <summary>
    /// The specific action being requested (e.g., "edit", "refresh").
    /// </summary>
    public string Action { get; }

    /// <summary>
    /// A dictionary containing any additional data attributes sent from the client.
    /// </summary>
    public Dictionary<string, object> Parameters { get; }
    public Guid TraceId { get; } = Guid.NewGuid();
    public List<TraceHop> History { get; } = new();

    public UIInteractionCommand(string ownerId, string action, Dictionary<string, object> additionalData)
    {
        OwnerId = ownerId;
        Action = action;
        Name = $"{ownerId}.UI.{action}";
        Parameters = additionalData;
    }
}