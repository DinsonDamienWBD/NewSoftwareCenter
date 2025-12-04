using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SoftwareCenter.Host;

/// <summary>
/// Represents the expected JSON body for a UI interaction request.
/// </summary>
public class InteractionRequest
{
    [Required]
    public string OwnerId { get; set; }

    [Required]
    public string Action { get; set; }

    // Captures all other properties from the JSON payload.
    [System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string, object> AdditionalData { get; set; }
}