using DataWarehouse.SDK.Contracts;
using Microsoft.Extensions.Logging;

namespace DataWarehouse.Kernel.Engine
{
    /// <summary>
    /// Analyzes hardware resources and execution environment to determine the optimal OperatingMode.
    /// Acts as the "Sorting Hat" for the Data Warehouse.
    /// </summary>
    public class RuntimeOptimizer
    {
        private readonly ILogger _logger;

        /// <summary>
        /// Gets the calculated operating mode.
        /// </summary>
        public OperatingMode CurrentMode { get; private set; }

        /// <summary>
        /// Initializes the optimizer and runs detection logic.
        /// </summary>
        /// <param name="logger">The system logger.</param>
        public RuntimeOptimizer(ILogger<RuntimeOptimizer> logger)
        {
            _logger = logger;
            CurrentMode = DetectEnvironment();
            _logger.LogInformation("[RuntimeOptimizer] Hardware Scan Complete. Mode: {CurrentMode}", CurrentMode);
        }

        private static OperatingMode DetectEnvironment()
        {
            // 1. Check for Containerization (Docker/K8s)
            bool isContainer = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";
            bool isK8s = Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST") != null;

            if (isK8s || isContainer)
            {
                return OperatingMode.Hyperscale;
            }

            // 2. Hardware Resource Check
            int processorCount = Environment.ProcessorCount;
            long memoryBytes = GetTotalMemory(); // Approximate

            // Heuristics for Server vs Laptop
            // Server: Usually > 8 Cores or explicit Server OS
            // Laptop: Usually <= 8 Cores

            if (processorCount > 16)
            {
                return OperatingMode.Server;
            }

            if (processorCount > 4)
            {
                return OperatingMode.Workstation;
            }

            // Default to Laptop for lower specs
            return OperatingMode.Laptop;
        }

        /// <summary>
        /// Gets the maximum recommended concurrency for the current mode.
        /// </summary>
        /// <returns>Number of parallel threads.</returns>
        public int GetMaxConcurrency()
        {
            return CurrentMode switch
            {
                OperatingMode.Laptop => MathUtils.Max(2, Environment.ProcessorCount / 2), // Conservative
                OperatingMode.Workstation => Environment.ProcessorCount - 1,         // Leave 1 for UI
                OperatingMode.Server => Environment.ProcessorCount * 2,              // Saturation
                OperatingMode.Hyperscale => Environment.ProcessorCount * 4,          // High IO wait tolerance
                _ => 2
            };
        }

        /// <summary>
        /// Determines if the Persistent Index (SQLite/Postgres) should be enforced.
        /// </summary>
        public static bool ShouldUsePersistentIndex()
        {
            // In Hyperscale (Containers), local storage might be ephemeral.
            // If we don't have a Remote DB configured, we might be forced into Volatile mode.
            // However, generally, DW implies persistence.

            // For Laptop/Workstation/Server: Always Yes.
            return true;
        }

        private static long GetTotalMemory()
        {
            try
            {
                return GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
            }
            catch
            {
                return 1024L * 1024L * 1024L; // Default 1GB if unknown
            }
        }
    }
}