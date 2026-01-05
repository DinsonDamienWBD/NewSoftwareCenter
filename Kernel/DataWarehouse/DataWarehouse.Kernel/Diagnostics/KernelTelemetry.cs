using System.Diagnostics;

namespace DataWarehouse.Kernel.Diagnostics
{
    /// <summary>
    /// Central Telemetry Hub for the Data Warehouse Kernel.
    /// Manages OpenTelemetry Activity Sources for distributed tracing.
    /// </summary>
    public static class KernelTelemetry
    {
        /// <summary>
        /// The Service Name for OpenTelemetry.
        /// </summary>
        public const string ServiceName = "DataWarehouse.Kernel";

        /// <summary>
        /// The Service Version.
        /// </summary>
        public const string ServiceVersion = "5.1.0-Silver";

        /// <summary>
        /// The main Activity Source. Use this to start spans.
        /// </summary>
        public static readonly ActivitySource Source = new(ServiceName, ServiceVersion);

        /// <summary>
        /// Starts a new trace span.
        /// </summary>
        /// <param name="name">Operation name (e.g. "StoreBlob").</param>
        /// <returns>The created Activity (Span).</returns>
        public static Activity? StartActivity(string name)
        {
            return Source.StartActivity(name);
        }
    }
}