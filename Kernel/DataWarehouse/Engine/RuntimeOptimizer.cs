using Microsoft.Extensions.Logging;

namespace DataWarehouse.Engine
{
    /// <summary>
    /// Operating mode based on hardware resources
    /// </summary>
    public enum OperatingMode 
    { 
        /// <summary>
        /// AC or Battery power - laptop mode
        /// </summary>
        LowPower, 

        /// <summary>
        /// AC power - desktop mode
        /// </summary>
        Desktop, 

        /// <summary>
        /// Always on, almost no throttling - server mode
        /// </summary>
        Server, 

        /// <summary>
        /// Hyperscale cloud infrastructure or datacenters mode
        /// </summary>
        Hyperscale 
    }

    /// <summary>
    /// Optimize features at runtime
    /// </summary>
    public class RuntimeOptimizer
    {
        private readonly ILogger _logger;

        /// <summary>
        /// Get current mode
        /// </summary>
        public OperatingMode CurrentMode { get; private set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logger"></param>
        public RuntimeOptimizer(ILogger logger)
        {
            _logger = logger;
            DetectEnvironment();
        }

        private void DetectEnvironment()
        {
            // 1. Core Count Check
            int cores = Environment.ProcessorCount;

            // 2. Memory Check (Heuristic)
            long memory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024); // MB

            // 3. Simple Heuristic Logic
            if (cores <= 4 || memory < 8192)
            {
                CurrentMode = OperatingMode.LowPower; // "Laptop Mode"
            }
            else if (cores <= 16)
            {
                CurrentMode = OperatingMode.Desktop; // "Workstation Mode"
            }
            else
            {
                CurrentMode = OperatingMode.Server; // "Data Center Mode"
            }

            _logger.LogInformation($"Environment Detected: {CurrentMode} (Cores: {cores}, Mem: {memory}MB)");
        }

        /// <summary>
        /// Enable or disable deduplication based on hardware resources
        /// Only hash on powerful machines
        /// </summary>
        /// <returns></returns>
        public bool ShouldEnableDeduplication() => CurrentMode >= OperatingMode.Server;

        /// <summary>
        /// Enable or disable background tiering based on hardware resources
        /// Only do tiering on at least desktop level machines
        /// </summary>
        /// <returns></returns>
        public bool ShouldEnableBackgroundTiering() => CurrentMode >= OperatingMode.Desktop;

        /// <summary>
        /// Get maximum concurrency
        /// </summary>
        /// <returns></returns>
        public int GetMaxConcurrency() => CurrentMode switch
        {
            OperatingMode.LowPower => 2,
            OperatingMode.Desktop => 8,
            OperatingMode.Server => 64,
            _ => 2
        };

        /// <summary>
        /// Set index storage option
        /// </summary>
        /// <returns></returns>
        public bool ShouldUsePersistentIndex()
        {
            // DECISION LOGIC:
            // 1. If we are in "LowPower" (2 cores, <8GB RAM), we might prefer InMemory speed 
            //    BUT we risk losing data. 
            // 2. Actually, for a Data Warehouse, Persistence is usually critical.
            //    So we default to Persistent (SQLite) unless we are in a purely ephemeral environment.

            // Check for "Container Mode" (Ephemeral Filesystem)
            bool isContainer = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";

            if (isContainer && CurrentMode == OperatingMode.LowPower)
            {
                // We are likely a throwaway worker process. 
                // Use In-Memory for max speed; data dies with the container.
                return false;
            }

            // Default: Everyone (Laptop, Desktop, Server) gets SQLite.
            // Why? Because nobody wants to re-index 1TB of files after a reboot.
            return true;
        }
    }
}