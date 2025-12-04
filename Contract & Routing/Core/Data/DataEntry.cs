using System;

namespace SoftwareCenter.Core.Data
{
    /// <summary>
    /// A rich wrapper around stored data.
    /// Provides accountability (Who saved this?) and validity checks (When was this saved?).
    /// </summary>
    /// <typeparam name="T">The type of the payload.</typeparam>
    public class DataEntry<T>
    {
        /// <summary>
        /// The actual data payload.
        /// </summary>
        public T? Value { get; set; }

        /// <summary>
        /// UTC Timestamp of the last write operation.
        /// </summary>
        public DateTime LastUpdated { get; set; }

        /// <summary>
        /// The ID of the module that owns/wrote this data.
        /// </summary>
        public string SourceId { get; set; } = string.Empty;

        /// <summary>
        /// The fully qualified type name of the data (safety check for deserialization).
        /// </summary>
        public string DataType { get; set; } = string.Empty;

        /// <summary>
        /// The Trace ID of the command/operation that caused this data change.
        /// Links the data state back to the specific user action.
        /// </summary>
        public Guid TraceId { get; set; }
    }
}