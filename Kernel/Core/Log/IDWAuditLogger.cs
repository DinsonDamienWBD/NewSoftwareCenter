namespace Core.Log
{
    public interface IDWAuditLogger
    {
        /// <summary>
        /// Logs a data access event.
        /// This method must never throw exceptions (logging failures shouldn't crash the app).
        /// </summary>
        Task LogAsync(AuditAction action, string roomId, string key, string user, string details = "");
    }
}
