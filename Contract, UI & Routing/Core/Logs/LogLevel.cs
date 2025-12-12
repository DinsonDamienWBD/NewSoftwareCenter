namespace SoftwareCenter.Core.Logs
{
    /// <summary>
    /// Defines the severity level of a log entry.
    /// </summary>
    public enum LogLevel
    {
        /// <summary>
        /// Tracing information and debugging payloads.
        /// </summary>
        Trace = 0,
        /// <summary>
        /// Information that is useful for debugging.
        /// </summary>
        Debug = 1,
        /// <summary>
        /// General application flow information.
        /// </summary>
        Information = 2,
        /// <summary>
        /// Unexpected events or deviations in expected flow.
        /// </summary>
        Warning = 3,
        /// <summary>
        /// Critical errors that prevent normal application flow.
        /// </summary>
        Error = 4,
        /// <summary>
        /// Fatal errors that cause the application to terminate.
        /// </summary>
        Critical = 5
    }
}
