using System;
using SoftwareCenter.Core.Diagnostics;

namespace SoftwareCenter.Kernel.Data
{
    /// <summary>
    /// Represents an audit record for an operation performed on the Global Data Store.
    /// </summary>
    public class AuditRecord
    {
        /// <summary>
        /// Gets or sets the unique ID of the audit record.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the key of the data item that was affected.
        /// </summary>
        public string DataKey { get; set; }

        /// <summary>
        /// Gets or sets the type of operation performed (e.g., "Get", "Set", "Delete", "ChangeOwner").
        /// </summary>
        public string OperationType { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the operation.
        /// </summary>
        public DateTimeOffset Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the TraceId associated with the operation, linking it to a full pipeline.
        /// </summary>
        public Guid TraceId { get; set; }

        /// <summary>
        /// Gets or sets the ModuleId that initiated the operation.
        /// </summary>
        public string InitiatingModuleId { get; set; }

        /// <summary>
        /// Gets or sets additional contextual data about the operation (e.g., old value, new value).
        /// </summary>
        public string Context { get; set; } = string.Empty;
    }
}
