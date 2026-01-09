using DataWarehouse.SDK.Contracts;
using System.Collections.Concurrent;

namespace DataWarehouse.Kernel.Distributed
{
    /// <summary>
    /// Coordinates data replication and synchronization across multiple geographic regions.
    /// Provides disaster recovery, data locality, and compliance with regional data regulations.
    /// </summary>
    public class MultiRegionCoordinator
    {
        private readonly IKernelContext _context;
        private readonly ConcurrentDictionary<string, RegionNode> _regions = new();
        private readonly ConcurrentDictionary<string, ReplicationPolicy> _replicationPolicies = new();
        private readonly object _lock = new();

        // Configuration
        private string _primaryRegion = string.Empty;
        private ReplicationStrategy _defaultStrategy = ReplicationStrategy.AsyncReplication;
        private int _quorumSize = 2; // Minimum regions for write confirmation

        public MultiRegionCoordinator(IKernelContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Initialize multi-region configuration.
        /// </summary>
        public async Task InitializeAsync(MultiRegionConfig config)
        {
            _context.LogInfo("[MultiRegion] Initializing multi-region coordinator...");

            _primaryRegion = config.PrimaryRegion;
            _defaultStrategy = config.DefaultStrategy;
            _quorumSize = config.QuorumSize;

            // Register all regions
            foreach (var regionConfig in config.Regions)
            {
                var region = new RegionNode
                {
                    RegionId = regionConfig.RegionId,
                    Name = regionConfig.Name,
                    Endpoint = regionConfig.Endpoint,
                    IsPrimary = regionConfig.RegionId == _primaryRegion,
                    Priority = regionConfig.Priority,
                    Status = RegionStatus.Initializing,
                    LastHealthCheck = DateTime.UtcNow
                };

                _regions[regionConfig.RegionId] = region;
                _context.LogInfo($"[MultiRegion] Registered region: {region.Name} ({region.RegionId})");
            }

            // Perform initial health check
            await PerformHealthCheckAsync();

            _context.LogInfo($"[MultiRegion] Initialized with {_regions.Count} regions, primary: {_primaryRegion}");
        }

        /// <summary>
        /// Write data with multi-region replication.
        /// </summary>
        public async Task<WriteResult> WriteAsync(string key, byte[] data, ReplicationPolicy? policy = null)
        {
            var effectivePolicy = policy ?? GetDefaultPolicy();
            var writeId = Guid.NewGuid().ToString();

            _context.LogDebug($"[MultiRegion] Write {writeId}: {key} with strategy {effectivePolicy.Strategy}");

            var result = new WriteResult
            {
                WriteId = writeId,
                Key = key,
                Timestamp = DateTime.UtcNow
            };

            try
            {
                switch (effectivePolicy.Strategy)
                {
                    case ReplicationStrategy.SyncReplication:
                        result = await SyncWriteAsync(writeId, key, data, effectivePolicy);
                        break;

                    case ReplicationStrategy.AsyncReplication:
                        result = await AsyncWriteAsync(writeId, key, data, effectivePolicy);
                        break;

                    case ReplicationStrategy.QuorumReplication:
                        result = await QuorumWriteAsync(writeId, key, data, effectivePolicy);
                        break;

                    case ReplicationStrategy.PrimaryOnly:
                        result = await PrimaryOnlyWriteAsync(writeId, key, data);
                        break;

                    default:
                        throw new NotSupportedException($"Strategy {effectivePolicy.Strategy} not supported");
                }

                _context.LogInfo($"[MultiRegion] Write {writeId} completed: {result.ReplicatedRegions}/{result.TargetRegions} regions");
                return result;
            }
            catch (Exception ex)
            {
                _context.LogError($"[MultiRegion] Write {writeId} failed", ex);
                result.Success = false;
                result.Error = ex.Message;
                return result;
            }
        }

        /// <summary>
        /// Read data with regional failover.
        /// </summary>
        public async Task<ReadResult> ReadAsync(string key, ReadPolicy? policy = null)
        {
            var effectivePolicy = policy ?? new ReadPolicy { PreferLocal = true };
            var readId = Guid.NewGuid().ToString();

            _context.LogDebug($"[MultiRegion] Read {readId}: {key}");

            // Determine read order based on policy
            var orderedRegions = GetReadOrderedRegions(effectivePolicy);

            foreach (var region in orderedRegions)
            {
                if (region.Status != RegionStatus.Healthy)
                {
                    _context.LogWarning($"[MultiRegion] Skipping unhealthy region: {region.Name}");
                    continue;
                }

                try
                {
                    var data = await ReadFromRegionAsync(region, key);
                    _context.LogDebug($"[MultiRegion] Read {readId} successful from {region.Name}");

                    return new ReadResult
                    {
                        ReadId = readId,
                        Key = key,
                        Data = data,
                        SourceRegion = region.RegionId,
                        Success = true,
                        Timestamp = DateTime.UtcNow
                    };
                }
                catch (Exception ex)
                {
                    _context.LogWarning($"[MultiRegion] Read from {region.Name} failed: {ex.Message}");
                    // Try next region
                }
            }

            _context.LogError($"[MultiRegion] Read {readId} failed from all regions");
            return new ReadResult
            {
                ReadId = readId,
                Key = key,
                Success = false,
                Error = "Data not available in any region"
            };
        }

        /// <summary>
        /// Synchronous replication - write to all regions before returning.
        /// </summary>
        private async Task<WriteResult> SyncWriteAsync(string writeId, string key, byte[] data, ReplicationPolicy policy)
        {
            var targetRegions = GetTargetRegions(policy);
            var tasks = new List<Task<(string RegionId, bool Success, string? Error)>>();

            foreach (var region in targetRegions)
            {
                tasks.Add(WriteToRegionAsync(region, key, data));
            }

            var results = await Task.WhenAll(tasks);
            var successCount = results.Count(r => r.Success);

            return new WriteResult
            {
                WriteId = writeId,
                Key = key,
                Success = successCount == targetRegions.Count,
                ReplicatedRegions = successCount,
                TargetRegions = targetRegions.Count,
                RegionResults = results.ToDictionary(r => r.RegionId, r => r.Success),
                Timestamp = DateTime.UtcNow,
                Error = successCount < targetRegions.Count ? "Some regions failed" : null
            };
        }

        /// <summary>
        /// Asynchronous replication - write to primary, replicate to others in background.
        /// </summary>
        private async Task<WriteResult> AsyncWriteAsync(string writeId, string key, byte[] data, ReplicationPolicy policy)
        {
            var primaryRegion = _regions[_primaryRegion];
            var writeTask = WriteToRegionAsync(primaryRegion, key, data);
            var primaryResult = await writeTask;

            if (!primaryResult.Success)
            {
                return new WriteResult
                {
                    WriteId = writeId,
                    Key = key,
                    Success = false,
                    ReplicatedRegions = 0,
                    TargetRegions = 1,
                    Error = primaryResult.Error,
                    Timestamp = DateTime.UtcNow
                };
            }

            // Background replication to secondary regions
            var secondaryRegions = GetTargetRegions(policy).Where(r => r.RegionId != _primaryRegion).ToList();
            _ = Task.Run(async () =>
            {
                foreach (var region in secondaryRegions)
                {
                    try
                    {
                        await WriteToRegionAsync(region, key, data);
                        _context.LogDebug($"[MultiRegion] Async replication to {region.Name} completed");
                    }
                    catch (Exception ex)
                    {
                        _context.LogError($"[MultiRegion] Async replication to {region.Name} failed", ex);
                    }
                }
            });

            return new WriteResult
            {
                WriteId = writeId,
                Key = key,
                Success = true,
                ReplicatedRegions = 1, // Only primary confirmed
                TargetRegions = secondaryRegions.Count + 1,
                Timestamp = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Quorum replication - write succeeds when quorum of regions confirm.
        /// </summary>
        private async Task<WriteResult> QuorumWriteAsync(string writeId, string key, byte[] data, ReplicationPolicy policy)
        {
            var targetRegions = GetTargetRegions(policy);
            var tasks = new List<Task<(string RegionId, bool Success, string? Error)>>();

            foreach (var region in targetRegions)
            {
                tasks.Add(WriteToRegionAsync(region, key, data));
            }

            // Wait for quorum or all to complete
            var completedTasks = new List<Task<(string RegionId, bool Success, string? Error)>>();
            var successCount = 0;

            while (tasks.Count > 0)
            {
                var completed = await Task.WhenAny(tasks);
                tasks.Remove(completed);
                completedTasks.Add(completed);

                var result = await completed;
                if (result.Success) successCount++;

                // Check if quorum reached
                if (successCount >= _quorumSize)
                {
                    _context.LogInfo($"[MultiRegion] Quorum reached: {successCount}/{_quorumSize}");
                    break;
                }
            }

            // Wait for remaining tasks to complete (but don't block return)
            _ = Task.WhenAll(tasks);

            var allResults = await Task.WhenAll(completedTasks);

            return new WriteResult
            {
                WriteId = writeId,
                Key = key,
                Success = successCount >= _quorumSize,
                ReplicatedRegions = successCount,
                TargetRegions = targetRegions.Count,
                RegionResults = allResults.ToDictionary(r => r.RegionId, r => r.Success),
                Timestamp = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Primary-only write - no replication.
        /// </summary>
        private async Task<WriteResult> PrimaryOnlyWriteAsync(string writeId, string key, byte[] data)
        {
            var primaryRegion = _regions[_primaryRegion];
            var result = await WriteToRegionAsync(primaryRegion, key, data);

            return new WriteResult
            {
                WriteId = writeId,
                Key = key,
                Success = result.Success,
                ReplicatedRegions = result.Success ? 1 : 0,
                TargetRegions = 1,
                Error = result.Error,
                Timestamp = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Write data to a specific region.
        /// </summary>
        private async Task<(string RegionId, bool Success, string? Error)> WriteToRegionAsync(
            RegionNode region, string key, byte[] data)
        {
            try
            {
                // Simulate network write to region
                // In production, this would use gRPC/HTTP to remote DataWarehouse instance
                await Task.Delay(50); // Simulate network latency

                region.LastWriteTime = DateTime.UtcNow;
                return (region.RegionId, true, null);
            }
            catch (Exception ex)
            {
                region.FailureCount++;
                return (region.RegionId, false, ex.Message);
            }
        }

        /// <summary>
        /// Read data from a specific region.
        /// </summary>
        private async Task<byte[]> ReadFromRegionAsync(RegionNode region, string key)
        {
            // Simulate network read from region
            await Task.Delay(30); // Simulate network latency
            region.LastReadTime = DateTime.UtcNow;

            // In production, this would use gRPC/HTTP to fetch data
            return Array.Empty<byte>(); // Placeholder
        }

        /// <summary>
        /// Get target regions based on replication policy.
        /// </summary>
        private List<RegionNode> GetTargetRegions(ReplicationPolicy policy)
        {
            if (policy.TargetRegions?.Any() == true)
            {
                return policy.TargetRegions
                    .Select(id => _regions.GetValueOrDefault(id))
                    .Where(r => r != null)
                    .Cast<RegionNode>()
                    .ToList();
            }

            // Default: all healthy regions
            return _regions.Values.Where(r => r.Status == RegionStatus.Healthy).ToList();
        }

        /// <summary>
        /// Get ordered list of regions for read operations.
        /// </summary>
        private List<RegionNode> GetReadOrderedRegions(ReadPolicy policy)
        {
            var regions = _regions.Values.ToList();

            if (policy.PreferLocal && !string.IsNullOrEmpty(policy.LocalRegion))
            {
                // Put local region first
                regions = regions.OrderBy(r => r.RegionId == policy.LocalRegion ? 0 : 1)
                                .ThenBy(r => r.Priority)
                                .ToList();
            }
            else
            {
                // Order by priority and health
                regions = regions.OrderBy(r => r.IsPrimary ? 0 : 1)
                                .ThenBy(r => r.Priority)
                                .ThenBy(r => r.FailureCount)
                                .ToList();
            }

            return regions;
        }

        /// <summary>
        /// Perform health check on all regions.
        /// </summary>
        public async Task PerformHealthCheckAsync()
        {
            _context.LogDebug("[MultiRegion] Performing health check...");

            var tasks = _regions.Values.Select(async region =>
            {
                try
                {
                    // Simulate health check ping
                    await Task.Delay(20);

                    region.Status = RegionStatus.Healthy;
                    region.LastHealthCheck = DateTime.UtcNow;
                    region.ConsecutiveFailures = 0;
                }
                catch (Exception ex)
                {
                    region.ConsecutiveFailures++;
                    region.Status = region.ConsecutiveFailures >= 3 ? RegionStatus.Failed : RegionStatus.Degraded;
                    _context.LogWarning($"[MultiRegion] Health check failed for {region.Name}: {ex.Message}");
                }
            });

            await Task.WhenAll(tasks);

            var healthy = _regions.Values.Count(r => r.Status == RegionStatus.Healthy);
            _context.LogInfo($"[MultiRegion] Health check complete: {healthy}/{_regions.Count} healthy");
        }

        /// <summary>
        /// Get status of all regions.
        /// </summary>
        public MultiRegionStatus GetStatus()
        {
            return new MultiRegionStatus
            {
                PrimaryRegion = _primaryRegion,
                TotalRegions = _regions.Count,
                HealthyRegions = _regions.Values.Count(r => r.Status == RegionStatus.Healthy),
                DegradedRegions = _regions.Values.Count(r => r.Status == RegionStatus.Degraded),
                FailedRegions = _regions.Values.Count(r => r.Status == RegionStatus.Failed),
                Regions = _regions.Values.Select(r => new RegionStatusInfo
                {
                    RegionId = r.RegionId,
                    Name = r.Name,
                    Status = r.Status,
                    IsPrimary = r.IsPrimary,
                    LastHealthCheck = r.LastHealthCheck,
                    FailureCount = r.FailureCount
                }).ToList(),
                Timestamp = DateTime.UtcNow
            };
        }

        private ReplicationPolicy GetDefaultPolicy()
        {
            return new ReplicationPolicy
            {
                Strategy = _defaultStrategy
            };
        }
    }

    public class RegionNode
    {
        public required string RegionId { get; init; }
        public required string Name { get; init; }
        public required string Endpoint { get; init; }
        public bool IsPrimary { get; init; }
        public int Priority { get; init; }
        public RegionStatus Status { get; set; }
        public DateTime LastHealthCheck { get; set; }
        public DateTime? LastWriteTime { get; set; }
        public DateTime? LastReadTime { get; set; }
        public int FailureCount { get; set; }
        public int ConsecutiveFailures { get; set; }
    }

    public enum RegionStatus
    {
        Initializing,
        Healthy,
        Degraded,
        Failed
    }

    public class MultiRegionConfig
    {
        public required string PrimaryRegion { get; init; }
        public ReplicationStrategy DefaultStrategy { get; init; } = ReplicationStrategy.AsyncReplication;
        public int QuorumSize { get; init; } = 2;
        public required List<RegionConfig> Regions { get; init; }
    }

    public class RegionConfig
    {
        public required string RegionId { get; init; }
        public required string Name { get; init; }
        public required string Endpoint { get; init; }
        public int Priority { get; init; } = 100;
    }

    public class ReplicationPolicy
    {
        public ReplicationStrategy Strategy { get; init; } = ReplicationStrategy.AsyncReplication;
        public List<string>? TargetRegions { get; init; }
        public int MinReplicas { get; init; } = 2;
    }

    public enum ReplicationStrategy
    {
        SyncReplication,    // Wait for all regions
        AsyncReplication,   // Write to primary, replicate in background
        QuorumReplication,  // Wait for quorum of regions
        PrimaryOnly         // No replication
    }

    public class ReadPolicy
    {
        public bool PreferLocal { get; init; } = true;
        public string? LocalRegion { get; init; }
        public bool AllowStaleReads { get; init; } = false;
    }

    public class WriteResult
    {
        public required string WriteId { get; init; }
        public required string Key { get; init; }
        public bool Success { get; set; }
        public int ReplicatedRegions { get; set; }
        public int TargetRegions { get; set; }
        public Dictionary<string, bool>? RegionResults { get; set; }
        public string? Error { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class ReadResult
    {
        public required string ReadId { get; init; }
        public required string Key { get; init; }
        public byte[]? Data { get; init; }
        public string? SourceRegion { get; init; }
        public bool Success { get; set; }
        public string? Error { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class MultiRegionStatus
    {
        public required string PrimaryRegion { get; init; }
        public int TotalRegions { get; init; }
        public int HealthyRegions { get; init; }
        public int DegradedRegions { get; init; }
        public int FailedRegions { get; init; }
        public List<RegionStatusInfo> Regions { get; init; } = [];
        public DateTime Timestamp { get; init; }
    }

    public class RegionStatusInfo
    {
        public required string RegionId { get; init; }
        public required string Name { get; init; }
        public RegionStatus Status { get; init; }
        public bool IsPrimary { get; init; }
        public DateTime LastHealthCheck { get; init; }
        public int FailureCount { get; init; }
    }
}
