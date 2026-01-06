using DataWarehouse.SDK.AI.Runtime;
using DataWarehouse.SDK.Primitives;
using DataWarehouse.SDK.Security;
using Microsoft.Json.Schema;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace DataWarehouse.SDK.Contracts
{
    public class IPluginCapability
    {
        /// <summary>
        /// Gets the unique identifier of the capability represented by this instance.
        /// E.g. "storage.local.save"
        /// </summary>
        string? CapabilityId { get; }

        /// <summary>
        /// Gets the display name for the operation or action.
        /// E.g. "Save Blob to Local Storage"
        /// </summary>
        string? DisplayName { get; }

        /// <summary>
        /// Gets a human-readable AI-friendly description of the object or operation.
        /// E.g. "Saves a binary large object (BLOB) to the local file system storage."
        /// </summary>
        string? Description { get; }

        /// <summary>
        /// Gets the category that describes the type of capability, such as storage, security, or transform.
        /// E.g. CapabilityCategory.Storage
        /// </summary>
        CapabilityCategory Category { get; }

        /// <summary>
        /// Gets the JSON schema that defines the structure and validation rules for the parameters.
        /// </summary>
        JsonSchema? ParameterSchema { get; }

        /// <summary>
        /// Gets the permission required to access the associated resource or perform the related operation.
        /// </summary>
        Permission RequiredPermission { get; }

        /// <summary>
        /// Gets a value indicating whether the operation requires approval before it can be performed.
        /// </summary>
        bool RequiresApproval { get; }

        Task<CapabilityResult> ExecuteAsync(
            Dictionary<string, object> parameters,
            IExecutionContext context
        );
    }
}
