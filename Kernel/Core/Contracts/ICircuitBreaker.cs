namespace Core.Contracts
{
    /// <summary>
    /// Circuit breaker
    /// </summary>
    public interface ICircuitBreaker
    {
        /// <summary>
        /// Check if resource is open
        /// </summary>
        /// <param name="resourceName"></param>
        /// <returns></returns>
        bool IsOpen(string resourceName);

        /// <summary>
        /// Report success
        /// </summary>
        /// <param name="resourceName"></param>
        void ReportSuccess(string resourceName);

        /// <summary>
        /// Report failure
        /// </summary>
        /// <param name="resourceName"></param>
        void ReportFailure(string resourceName);
    }
}