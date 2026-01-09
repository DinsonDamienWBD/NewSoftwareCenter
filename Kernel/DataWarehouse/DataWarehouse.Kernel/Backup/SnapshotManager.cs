using DataWarehouse.SDK.Contracts;
using DataWarehouse.SDK.Primitives;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DataWarehouse.Kernel.Backup
{
    /// <summary>
    /// Time Machine-style immutable snapshot manager.
    /// Creates point-in-time snapshots with versioning and granular metadata.
    /// </summary>
    public class SnapshotManager
    {
        private readonly IKernelContext _context;
        private readonly IMetadataIndex _index;
        private readonly string _snapshotDirectory;
        private readonly ConcurrentDictionary<string, Snapshot> _snapshots = new();

        public SnapshotManager(IKernelContext context, IMetadataIndex index, string? snapshotDirectory = null)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _index = index ?? throw new ArgumentNullException(nameof(index));
            _snapshotDirectory = snapshotDirectory ?? Path.Combine(context.RootPath, "snapshots");
            Directory.CreateDirectory(_snapshotDirectory);
        }

        /// <summary>
        /// Initialize and load existing snapshots.
        /// </summary>
        public async Task InitializeAsync()
        {
            await LoadSnapshotsAsync();
            _context.LogInfo($"[Snapshot] Loaded {_snapshots.Count} snapshots from registry");
        }

        /// <summary>
        /// Create an immutable snapshot at specified granularity.
        /// </summary>
        public async Task<Snapshot> CreateSnapshotAsync(
            RestoreGranularity granularity,
            string? targetId = null,
            string? description = null,
            CancellationToken cancellationToken = default)
        {
            var snapshotId = Guid.NewGuid().ToString();
            var timestamp = DateTime.UtcNow;

            _context.LogInfo($"[Snapshot] Creating {granularity} snapshot: {snapshotId}");

            var snapshot = new Snapshot
            {
                SnapshotId = snapshotId,
                Granularity = granularity,
                TargetId = targetId,
                Timestamp = timestamp,
                Description = description ?? $"{granularity} snapshot at {timestamp:yyyy-MM-dd HH:mm:ss}",
                Status = SnapshotStatus.Creating,
                Manifests = new List<SnapshotManifest>()
            };

            try
            {
                // Create snapshot based on granularity
                switch (granularity)
                {
                    case RestoreGranularity.SingleFile:
                        if (string.IsNullOrEmpty(targetId))
                            throw new ArgumentException("TargetId required for SingleFile snapshot", nameof(targetId));
                        await CreateFileSnapshotAsync(snapshot, targetId, cancellationToken);
                        break;

                    case RestoreGranularity.Compartment:
                        if (string.IsNullOrEmpty(targetId))
                            throw new ArgumentException("TargetId required for Compartment snapshot", nameof(targetId));
                        await CreateCompartmentSnapshotAsync(snapshot, targetId, cancellationToken);
                        break;

                    case RestoreGranularity.Partition:
                        if (string.IsNullOrEmpty(targetId))
                            throw new ArgumentException("TargetId required for Partition snapshot", nameof(targetId));
                        await CreatePartitionSnapshotAsync(snapshot, targetId, cancellationToken);
                        break;

                    case RestoreGranularity.StorageLayer:
                        if (string.IsNullOrEmpty(targetId))
                            throw new ArgumentException("TargetId required for StorageLayer snapshot", nameof(targetId));
                        await CreateStorageLayerSnapshotAsync(snapshot, targetId, cancellationToken);
                        break;

                    case RestoreGranularity.StoragePool:
                        if (string.IsNullOrEmpty(targetId))
                            throw new ArgumentException("TargetId required for StoragePool snapshot", nameof(targetId));
                        await CreateStoragePoolSnapshotAsync(snapshot, targetId, cancellationToken);
                        break;

                    case RestoreGranularity.MultiplePools:
                        await CreateMultiplePoolsSnapshotAsync(snapshot, cancellationToken);
                        break;

                    case RestoreGranularity.CompleteInstance:
                        await CreateCompleteInstanceSnapshotAsync(snapshot, cancellationToken);
                        break;

                    default:
                        throw new NotSupportedException($"Granularity {granularity} not supported");
                }

                // Calculate snapshot hash for integrity verification
                snapshot.IntegrityHash = await CalculateSnapshotHashAsync(snapshot, cancellationToken);

                // Mark as immutable
                snapshot.Status = SnapshotStatus.Immutable;
                snapshot.CompletionTime = DateTime.UtcNow;
                snapshot.DurationMs = (long)(snapshot.CompletionTime.Value - snapshot.Timestamp).TotalMilliseconds;

                // Save snapshot metadata
                await SaveSnapshotMetadataAsync(snapshot);
                _snapshots[snapshotId] = snapshot;

                _context.LogInfo($"[Snapshot] Completed {granularity} snapshot: {snapshotId}, files: {snapshot.TotalFiles}, size: {FormatBytes(snapshot.TotalBytes)}");

                return snapshot;
            }
            catch (Exception ex)
            {
                _context.LogError($"[Snapshot] Failed to create {granularity} snapshot", ex);
                snapshot.Status = SnapshotStatus.Failed;
                snapshot.ErrorMessage = ex.Message;
                throw;
            }
        }

        /// <summary>
        /// Get snapshot by ID.
        /// </summary>
        public Snapshot? GetSnapshot(string snapshotId)
        {
            _snapshots.TryGetValue(snapshotId, out var snapshot);
            return snapshot;
        }

        /// <summary>
        /// List snapshots with optional filtering.
        /// </summary>
        public List<Snapshot> ListSnapshots(
            RestoreGranularity? granularity = null,
            DateTime? since = null,
            DateTime? until = null)
        {
            var snapshots = _snapshots.Values.AsEnumerable();

            if (granularity.HasValue)
                snapshots = snapshots.Where(s => s.Granularity == granularity.Value);

            if (since.HasValue)
                snapshots = snapshots.Where(s => s.Timestamp >= since.Value);

            if (until.HasValue)
                snapshots = snapshots.Where(s => s.Timestamp <= until.Value);

            return snapshots.OrderByDescending(s => s.Timestamp).ToList();
        }

        /// <summary>
        /// Delete a snapshot (if not protected).
        /// </summary>
        public async Task<bool> DeleteSnapshotAsync(string snapshotId)
        {
            if (!_snapshots.TryGetValue(snapshotId, out var snapshot))
                return false;

            if (snapshot.IsProtected)
            {
                _context.LogWarning($"[Snapshot] Cannot delete protected snapshot: {snapshotId}");
                return false;
            }

            try
            {
                // Delete snapshot data
                var snapshotPath = GetSnapshotPath(snapshotId);
                if (Directory.Exists(snapshotPath))
                {
                    Directory.Delete(snapshotPath, recursive: true);
                }

                _snapshots.TryRemove(snapshotId, out _);
                _context.LogInfo($"[Snapshot] Deleted snapshot: {snapshotId}");

                return true;
            }
            catch (Exception ex)
            {
                _context.LogError($"[Snapshot] Failed to delete snapshot {snapshotId}", ex);
                return false;
            }
        }

        /// <summary>
        /// Verify snapshot integrity using hash.
        /// </summary>
        public async Task<bool> VerifySnapshotIntegrityAsync(string snapshotId, CancellationToken cancellationToken = default)
        {
            if (!_snapshots.TryGetValue(snapshotId, out var snapshot))
                return false;

            try
            {
                var currentHash = await CalculateSnapshotHashAsync(snapshot, cancellationToken);
                var isValid = currentHash == snapshot.IntegrityHash;

                if (!isValid)
                {
                    _context.LogError($"[Snapshot] Integrity check failed for snapshot {snapshotId}");
                }

                return isValid;
            }
            catch (Exception ex)
            {
                _context.LogError($"[Snapshot] Integrity verification failed for snapshot {snapshotId}", ex);
                return false;
            }
        }

        #region Private Snapshot Creation Methods

        private async Task CreateFileSnapshotAsync(Snapshot snapshot, string manifestId, CancellationToken cancellationToken)
        {
            var manifest = await _index.GetManifestAsync(manifestId, cancellationToken);
            if (manifest == null)
                throw new InvalidOperationException($"Manifest {manifestId} not found");

            var snapshotPath = GetSnapshotPath(snapshot.SnapshotId);
            Directory.CreateDirectory(snapshotPath);

            var snapshotManifest = new SnapshotManifest
            {
                ManifestId = manifest.Id,
                RelativePath = manifest.RelativePath,
                SizeBytes = manifest.SizeBytes,
                Hash = manifest.ContentHash,
                Timestamp = manifest.CreatedAt
            };

            // Copy file data to snapshot
            var sourceProvider = _context.GetPlugin<IStorageProvider>();
            if (sourceProvider == null)
                throw new InvalidOperationException("No storage provider available");

            var targetFile = Path.Combine(snapshotPath, "data", manifest.RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);

            using var sourceStream = await sourceProvider.LoadAsync(new Uri($"{sourceProvider.Scheme}://{manifest.RelativePath}"));
            using var targetStream = File.Create(targetFile);
            await sourceStream.CopyToAsync(targetStream, cancellationToken);

            snapshot.Manifests.Add(snapshotManifest);
            snapshot.TotalFiles = 1;
            snapshot.TotalBytes = manifest.SizeBytes;
        }

        private async Task CreateCompartmentSnapshotAsync(Snapshot snapshot, string compartmentId, CancellationToken cancellationToken)
        {
            var manifests = await _index.QueryAsync($"compartment:{compartmentId}", cancellationToken: cancellationToken);
            await CreateSnapshotFromManifestsAsync(snapshot, manifests, cancellationToken);
        }

        private async Task CreatePartitionSnapshotAsync(Snapshot snapshot, string partitionId, CancellationToken cancellationToken)
        {
            var manifests = await _index.QueryAsync($"partition:{partitionId}", cancellationToken: cancellationToken);
            await CreateSnapshotFromManifestsAsync(snapshot, manifests, cancellationToken);
        }

        private async Task CreateStorageLayerSnapshotAsync(Snapshot snapshot, string layerId, CancellationToken cancellationToken)
        {
            var manifests = await _index.QueryAsync($"storage:{layerId}", cancellationToken: cancellationToken);
            await CreateSnapshotFromManifestsAsync(snapshot, manifests, cancellationToken);
        }

        private async Task CreateStoragePoolSnapshotAsync(Snapshot snapshot, string poolId, CancellationToken cancellationToken)
        {
            var manifests = await _index.QueryAsync($"pool:{poolId}", cancellationToken: cancellationToken);
            await CreateSnapshotFromManifestsAsync(snapshot, manifests, cancellationToken);
        }

        private async Task CreateMultiplePoolsSnapshotAsync(Snapshot snapshot, CancellationToken cancellationToken)
        {
            var manifests = await _index.QueryAsync("*", cancellationToken: cancellationToken);
            await CreateSnapshotFromManifestsAsync(snapshot, manifests, cancellationToken);
        }

        private async Task CreateCompleteInstanceSnapshotAsync(Snapshot snapshot, CancellationToken cancellationToken)
        {
            var manifests = await _index.QueryAsync("*", cancellationToken: cancellationToken);
            await CreateSnapshotFromManifestsAsync(snapshot, manifests, cancellationToken);

            // Also backup configuration and metadata
            await BackupConfigurationAsync(snapshot, cancellationToken);
            await BackupMetadataAsync(snapshot, cancellationToken);
        }

        private async Task CreateSnapshotFromManifestsAsync(
            Snapshot snapshot,
            List<Manifest> manifests,
            CancellationToken cancellationToken)
        {
            var snapshotPath = GetSnapshotPath(snapshot.SnapshotId);
            Directory.CreateDirectory(snapshotPath);

            var sourceProvider = _context.GetPlugin<IStorageProvider>();
            if (sourceProvider == null)
                throw new InvalidOperationException("No storage provider available");

            foreach (var manifest in manifests)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var snapshotManifest = new SnapshotManifest
                {
                    ManifestId = manifest.Id,
                    RelativePath = manifest.RelativePath,
                    SizeBytes = manifest.SizeBytes,
                    Hash = manifest.ContentHash,
                    Timestamp = manifest.CreatedAt
                };

                // Copy file data to snapshot
                var targetFile = Path.Combine(snapshotPath, "data", manifest.RelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);

                try
                {
                    using var sourceStream = await sourceProvider.LoadAsync(new Uri($"{sourceProvider.Scheme}://{manifest.RelativePath}"));
                    using var targetStream = File.Create(targetFile);
                    await sourceStream.CopyToAsync(targetStream, cancellationToken);

                    snapshot.Manifests.Add(snapshotManifest);
                    snapshot.TotalFiles++;
                    snapshot.TotalBytes += manifest.SizeBytes;
                }
                catch (Exception ex)
                {
                    _context.LogWarning($"[Snapshot] Failed to backup file {manifest.RelativePath}: {ex.Message}");
                }
            }
        }

        private async Task BackupConfigurationAsync(Snapshot snapshot, CancellationToken cancellationToken)
        {
            var configPath = Path.Combine(_context.RootPath, "Config");
            if (!Directory.Exists(configPath))
                return;

            var snapshotPath = GetSnapshotPath(snapshot.SnapshotId);
            var targetConfigPath = Path.Combine(snapshotPath, "Config");
            Directory.CreateDirectory(targetConfigPath);

            await CopyDirectoryAsync(configPath, targetConfigPath, cancellationToken);
        }

        private async Task BackupMetadataAsync(Snapshot snapshot, CancellationToken cancellationToken)
        {
            var metadataPath = Path.Combine(_context.RootPath, "Metadata");
            if (!Directory.Exists(metadataPath))
                return;

            var snapshotPath = GetSnapshotPath(snapshot.SnapshotId);
            var targetMetadataPath = Path.Combine(snapshotPath, "Metadata");
            Directory.CreateDirectory(targetMetadataPath);

            await CopyDirectoryAsync(metadataPath, targetMetadataPath, cancellationToken);
        }

        private async Task CopyDirectoryAsync(string sourceDir, string targetDir, CancellationToken cancellationToken)
        {
            foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var relativePath = Path.GetRelativePath(sourceDir, file);
                var targetFile = Path.Combine(targetDir, relativePath);

                Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
                File.Copy(file, targetFile, overwrite: true);
            }

            await Task.CompletedTask;
        }

        #endregion

        #region Snapshot Metadata Management

        private async Task<string> CalculateSnapshotHashAsync(Snapshot snapshot, CancellationToken cancellationToken)
        {
            using var sha256 = SHA256.Create();
            var sb = new StringBuilder();

            // Hash all manifest hashes in sorted order for deterministic result
            foreach (var manifest in snapshot.Manifests.OrderBy(m => m.ManifestId))
            {
                cancellationToken.ThrowIfCancellationRequested();
                sb.Append(manifest.Hash);
            }

            var combinedHash = sha256.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
            return Convert.ToHexString(combinedHash);
        }

        private async Task SaveSnapshotMetadataAsync(Snapshot snapshot)
        {
            var snapshotPath = GetSnapshotPath(snapshot.SnapshotId);
            var metadataFile = Path.Combine(snapshotPath, "snapshot.json");

            var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            await File.WriteAllTextAsync(metadataFile, json);
        }

        private async Task LoadSnapshotsAsync()
        {
            if (!Directory.Exists(_snapshotDirectory))
                return;

            foreach (var snapshotDir in Directory.GetDirectories(_snapshotDirectory))
            {
                var metadataFile = Path.Combine(snapshotDir, "snapshot.json");
                if (File.Exists(metadataFile))
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(metadataFile);
                        var snapshot = JsonSerializer.Deserialize<Snapshot>(json);
                        if (snapshot != null)
                        {
                            _snapshots[snapshot.SnapshotId] = snapshot;
                        }
                    }
                    catch (Exception ex)
                    {
                        _context.LogWarning($"[Snapshot] Failed to load snapshot metadata: {ex.Message}");
                    }
                }
            }
        }

        private string GetSnapshotPath(string snapshotId)
        {
            return Path.Combine(_snapshotDirectory, snapshotId);
        }

        private static string FormatBytes(long bytes)
        {
            string[] sizes = ["B", "KB", "MB", "GB", "TB"];
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        #endregion
    }

    /// <summary>
    /// Restore granularity levels (7 levels as specified).
    /// </summary>
    public enum RestoreGranularity
    {
        /// <summary>
        /// Single file restore.
        /// </summary>
        SingleFile = 1,

        /// <summary>
        /// Compartment-level restore (logical grouping).
        /// </summary>
        Compartment = 2,

        /// <summary>
        /// Partition-level restore (storage partition).
        /// </summary>
        Partition = 3,

        /// <summary>
        /// Storage layer restore (e.g., local, S3, IPFS).
        /// </summary>
        StorageLayer = 4,

        /// <summary>
        /// Storage pool restore (group of storage layers).
        /// </summary>
        StoragePool = 5,

        /// <summary>
        /// Multiple pools restore.
        /// </summary>
        MultiplePools = 6,

        /// <summary>
        /// Complete instance restore (everything).
        /// </summary>
        CompleteInstance = 7
    }

    public enum SnapshotStatus
    {
        Creating,
        Immutable,
        Failed
    }

    public class Snapshot
    {
        public required string SnapshotId { get; init; }
        public RestoreGranularity Granularity { get; init; }
        public string? TargetId { get; init; }
        public DateTime Timestamp { get; init; }
        public required string Description { get; init; }
        public SnapshotStatus Status { get; set; }
        public DateTime? CompletionTime { get; set; }
        public long DurationMs { get; set; }
        public string? IntegrityHash { get; set; }
        public int TotalFiles { get; set; }
        public long TotalBytes { get; set; }
        public bool IsProtected { get; set; }
        public string? ErrorMessage { get; set; }
        public required List<SnapshotManifest> Manifests { get; init; }
    }

    public class SnapshotManifest
    {
        public required string ManifestId { get; init; }
        public required string RelativePath { get; init; }
        public long SizeBytes { get; init; }
        public required string Hash { get; init; }
        public DateTime Timestamp { get; init; }
    }
}
