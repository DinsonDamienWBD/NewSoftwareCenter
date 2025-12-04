namespace SoftwareCenter.Core.Data
{
    /// <summary>
    /// Dictates how the Global Data Store handles persistence for a specific key.
    /// </summary>
    public enum DataPolicy
    {
        /// <summary>
        /// Stored in RAM only. Lost when the application closes.
        /// Use for: Session tokens, temporary UI state, caching.
        /// </summary>
        Transient = 0,

        /// <summary>
        /// Saved to disk (e.g., LiteDB/SQLite). Survives restarts.
        /// Use for: User settings, application config, long-term logs.
        /// </summary>
        Persistent = 1
    }
}