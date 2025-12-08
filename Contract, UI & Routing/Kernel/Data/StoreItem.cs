using System;
using System.Collections.Generic;
using SoftwareCenter.Kernel.Data; // Added for AccessPermissions

namespace SoftwareCenter.Kernel.Data
{
    /// <summary>
    /// A wrapper for data stored in the Global Data Store, containing metadata for ownership and auditing.
    /// </summary>
    public class StoreItem<T>
    {
        /// <summary>
        /// Gets or sets the unique identifier for this store item.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the key that uniquely identifies this piece of data logically.
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// Gets or sets the actual data payload.
        /// </summary>
        public T Value { get; set; }

        /// <summary>
        /// Gets or sets the ID of the module that currently owns this data.
        /// Only the owner can update or delete the data.
        /// </summary>
        public string OwnerModuleId { get; set; }

        /// <summary>
        /// Gets or sets a dictionary of module IDs and the access permissions they have been granted for this item.
        /// </summary>
        public Dictionary<string, AccessPermissions> SharedPermissions { get; set; } = new Dictionary<string, AccessPermissions>();

        /// <summary>
        /// Gets or sets the trace ID of the operation that created this data.
        /// </summary>
        public Guid CreatorTraceId { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the data was created.
        /// </summary>
        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the trace ID of the operation that last updated this data.
        /// </summary>
        public Guid LastUpdaterTraceId { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the data was last updated.
        /// </summary>
        public DateTimeOffset LastUpdatedAt { get; set; }
    }
}
