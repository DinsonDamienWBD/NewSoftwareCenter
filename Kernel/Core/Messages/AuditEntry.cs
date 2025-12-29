namespace Core.Messages
{
    /// <summary>
    /// Struct for audit diffs
    /// </summary>
    public class AuditEntry(string field, string? oldValue, string? newValue, string reason = "")
    {
        /// <summary>
        /// ID of the field
        /// </summary>
        public string Field { get; set; } = field;

        /// <summary>
        /// Old value of the field
        /// </summary>
        public string? OldValue { get; set; } = oldValue;

        /// <summary>
        /// New value of the field
        /// </summary>
        public string? NewValue { get; set; } = newValue;

        /// <summary>
        /// Reason for the change
        /// </summary>
        public string Reason { get; set; } = reason;
    }
}