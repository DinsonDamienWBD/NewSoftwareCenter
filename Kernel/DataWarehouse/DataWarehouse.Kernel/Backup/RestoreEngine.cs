using DataWarehouse.SDK.Contracts;
using DataWarehouse.SDK.Primitives;
using DataWarehouse.SDK.Security;
using System.Text.Json;

namespace DataWarehouse.Kernel.Backup
{
    /// <summary>
    /// Time Machine-style restore engine with 7 granularity levels.
    /// Supports point-in-time restore with permission validation.
    /// </summary>
    public class RestoreEngine(
        IKernelContext context,
        SnapshotManager snapshotManager,
        IMetadataIndex index,
        ISecurityProvider? securityProvider = null)
    {
        private readonly IKernelContext _context = context ?? throw new ArgumentNullException(nameof(context));
        private readonly SnapshotManager _snapshotManager = snapshotManager ?? throw new ArgumentNullException(nameof(snapshotManager));
        private readonly ISecurityProvider? _securityProvider = securityProvider;
        private readonly IMetadataIndex _index = index ?? throw new ArgumentNullException(nameof(index));

        /// <summary>
        /// Restore from snapshot with permission validation.
        /// </summary>
        public async Task<RestoreResult> RestoreAsync(
            string snapshotId,
            RestoreOptions options,
            ISecurityContext? securityContext = null,
            CancellationToken cancellationToken = default)
        {
            _context.LogInfo($"[Restore] Starting restore from snapshot: {snapshotId}");

            // Get snapshot
            var snapshot = _snapshotManager.GetSnapshot(snapshotId);
            if (snapshot == null)
            {
                return new RestoreResult
                {
                    Success = false,
                    ErrorMessage = $"Snapshot {snapshotId} not found"
                };
            }

            // Verify snapshot integrity
            if (options.VerifyIntegrity)
            {
                _context.LogInfo($"[Restore] Verifying snapshot integrity...");
                var isValid = await _snapshotManager.VerifySnapshotIntegrityAsync(snapshotId, cancellationToken);
                if (!isValid)
                {
                    return new RestoreResult
                    {
                        Success = false,
                        ErrorMessage = "Snapshot integrity check failed"
                    };
                }
            }

            // Validate permissions
            if (securityContext != null && _securityProvider != null)
            {
                var hasPermission = await ValidateRestorePermissionAsync(
                    snapshot,
                    options,
                    securityContext,
                    cancellationToken);

                if (!hasPermission)
                {
                    return new RestoreResult
                    {
                        Success = false,
                        ErrorMessage = "Insufficient permissions to restore this snapshot"
                    };
                }
            }

            try
            {
                var result = new RestoreResult
                {
                    SnapshotId = snapshotId,
                    Granularity = snapshot.Granularity,
                    StartTime = DateTime.UtcNow
                };

                // Perform restore based on granularity and options
                switch (snapshot.Granularity)
                {
                    case RestoreGranularity.SingleFile:
                        await RestoreSingleFileAsync(snapshot, options, result, cancellationToken);
                        break;

                    case RestoreGranularity.Compartment:
                        await RestoreCompartmentAsync(snapshot, options, result, cancellationToken);
                        break;

                    case RestoreGranularity.Partition:
                        await RestorePartitionAsync(snapshot, options, result, cancellationToken);
                        break;

                    case RestoreGranularity.StorageLayer:
                        await RestoreStorageLayerAsync(snapshot, options, result, cancellationToken);
                        break;

                    case RestoreGranularity.StoragePool:
                        await RestoreStoragePoolAsync(snapshot, options, result, cancellationToken);
                        break;

                    case RestoreGranularity.MultiplePools:
                        await RestoreMultiplePoolsAsync(snapshot, options, result, cancellationToken);
                        break;

                    case RestoreGranularity.CompleteInstance:
                        await RestoreCompleteInstanceAsync(snapshot, options, result, cancellationToken);
                        break;

                    default:
                        throw new NotSupportedException($"Granularity {snapshot.Granularity} not supported");
                }

                result.Success = true;
                result.EndTime = DateTime.UtcNow;
                result.DurationMs = (long)(result.EndTime.Value - result.StartTime).TotalMilliseconds;

                _context.LogInfo($"[Restore] Completed restore from snapshot {snapshotId}, restored {result.RestoredFiles} files ({FormatBytes(result.RestoredBytes)})");

                return result;
            }
            catch (Exception ex)
            {
                _context.LogError($"[Restore] Restore failed", ex);
                return new RestoreResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// List available restore points for a given target.
        /// </summary>
        public List<Snapshot> ListRestorePoints(
            RestoreGranularity? granularity = null,
            string? targetId = null,
            DateTime? since = null)
        {
            var snapshots = _snapshotManager.ListSnapshots(granularity, since);

            if (!string.IsNullOrEmpty(targetId))
            {
                snapshots = [.. snapshots.Where(s => s.TargetId == targetId)];
            }

            return snapshots;
        }

        /// <summary>
        /// Browse snapshot contents before restore.
        /// </summary>
        public async Task<SnapshotBrowseResult> BrowseSnapshotAsync(
            string snapshotId,
            string? path = null,
            CancellationToken cancellationToken = default)
        {
            var snapshot = _snapshotManager.GetSnapshot(snapshotId);
            if (snapshot == null)
            {
                return new SnapshotBrowseResult
                {
                    Success = false,
                    ErrorMessage = $"Snapshot {snapshotId} not found"
                };
            }

            var manifests = snapshot.Manifests;

            // Filter by path if provided
            if (!string.IsNullOrEmpty(path))
            {
                manifests = [.. manifests.Where(m => m.RelativePath.StartsWith(path, StringComparison.OrdinalIgnoreCase))];
            }

            return new SnapshotBrowseResult
            {
                Success = true,
                SnapshotId = snapshotId,
                Granularity = snapshot.Granularity,
                Timestamp = snapshot.Timestamp,
                TotalFiles = manifests.Count,
                TotalBytes = manifests.Sum(m => m.SizeBytes),
                Files = [.. manifests.Select(m => new SnapshotFileInfo
                {
                    Path = m.RelativePath,
                    SizeBytes = m.SizeBytes,
                    Hash = m.Hash,
                    Timestamp = m.Timestamp
                })]
            };
        }

        #region Private Restore Methods

        private async Task RestoreSingleFileAsync(
            Snapshot snapshot,
            RestoreOptions options,
            RestoreResult result,
            CancellationToken cancellationToken)
        {
            if (snapshot.Manifests.Count == 0)
                throw new InvalidOperationException("Snapshot contains no files");

            var manifest = snapshot.Manifests.First();
            await RestoreFileAsync(snapshot, manifest, options, result, cancellationToken);
        }

        private async Task RestoreCompartmentAsync(
            Snapshot snapshot,
            RestoreOptions options,
            RestoreResult result,
            CancellationToken cancellationToken)
        {
            await RestoreMultipleFilesAsync(snapshot, snapshot.Manifests, options, result, cancellationToken);
        }

        private async Task RestorePartitionAsync(
            Snapshot snapshot,
            RestoreOptions options,
            RestoreResult result,
            CancellationToken cancellationToken)
        {
            await RestoreMultipleFilesAsync(snapshot, snapshot.Manifests, options, result, cancellationToken);
        }

        private async Task RestoreStorageLayerAsync(
            Snapshot snapshot,
            RestoreOptions options,
            RestoreResult result,
            CancellationToken cancellationToken)
        {
            await RestoreMultipleFilesAsync(snapshot, snapshot.Manifests, options, result, cancellationToken);
        }

        private async Task RestoreStoragePoolAsync(
            Snapshot snapshot,
            RestoreOptions options,
            RestoreResult result,
            CancellationToken cancellationToken)
        {
            await RestoreMultipleFilesAsync(snapshot, snapshot.Manifests, options, result, cancellationToken);
        }

        private async Task RestoreMultiplePoolsAsync(
            Snapshot snapshot,
            RestoreOptions options,
            RestoreResult result,
            CancellationToken cancellationToken)
        {
            await RestoreMultipleFilesAsync(snapshot, snapshot.Manifests, options, result, cancellationToken);
        }

        private async Task RestoreCompleteInstanceAsync(
            Snapshot snapshot,
            RestoreOptions options,
            RestoreResult result,
            CancellationToken cancellationToken)
        {
            // Restore all files
            await RestoreMultipleFilesAsync(snapshot, snapshot.Manifests, options, result, cancellationToken);

            // Restore configuration
            await RestoreConfigurationAsync(snapshot, options, cancellationToken);

            // Restore metadata
            await RestoreMetadataAsync(snapshot, options, cancellationToken);
        }

        private async Task RestoreMultipleFilesAsync(
            Snapshot snapshot,
            List<SnapshotManifest> manifests,
            RestoreOptions options,
            RestoreResult result,
            CancellationToken cancellationToken)
        {
            foreach (var manifest in manifests)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    await RestoreFileAsync(snapshot, manifest, options, result, cancellationToken);
                }
                catch (Exception ex)
                {
                    _context.LogWarning($"[Restore] Failed to restore file {manifest.RelativePath}: {ex.Message}");
                    result.FailedFiles++;
                }
            }
        }

        private async Task RestoreFileAsync(
            Snapshot snapshot,
            SnapshotManifest manifest,
            RestoreOptions options,
            RestoreResult result,
            CancellationToken cancellationToken)
        {
            var snapshotPath = Path.Combine(_context.RootPath, "snapshots", snapshot.SnapshotId);
            var sourceFile = Path.Combine(snapshotPath, "data", manifest.RelativePath);

            if (!File.Exists(sourceFile))
            {
                _context.LogWarning($"[Restore] Source file not found: {sourceFile}");
                result.FailedFiles++;
                return;
            }

            // Determine target location
            string targetFile;
            if (!string.IsNullOrEmpty(options.TargetPath))
            {
                targetFile = Path.Combine(options.TargetPath, manifest.RelativePath);
            }
            else
            {
                targetFile = Path.Combine(_context.RootPath, "data", manifest.RelativePath);
            }

            // Handle conflicts
            if (File.Exists(targetFile) && !options.OverwriteExisting)
            {
                switch (options.ConflictResolution)
                {
                    case ConflictResolution.Skip:
                        result.SkippedFiles++;
                        return;

                    case ConflictResolution.CreateVersion:
                        targetFile = CreateVersionedPath(targetFile);
                        break;

                    case ConflictResolution.Rename:
                        targetFile = CreateRenamedPath(targetFile);
                        break;

                    case ConflictResolution.Fail:
                        throw new InvalidOperationException($"File already exists: {targetFile}");
                }
            }

            // Copy file
            Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
            File.Copy(sourceFile, targetFile, overwrite: options.OverwriteExisting);

            // Restore original timestamps if requested
            if (options.RestoreTimestamps)
            {
                File.SetCreationTimeUtc(targetFile, manifest.Timestamp);
                File.SetLastWriteTimeUtc(targetFile, manifest.Timestamp);
            }

            result.RestoredFiles++;
            result.RestoredBytes += manifest.SizeBytes;

            // Update metadata index if requested
            if (options.UpdateMetadataIndex)
            {
                var newManifest = new Manifest
                {
                    Id = Guid.NewGuid().ToString(),
                    RelativePath = manifest.RelativePath,
                    SizeBytes = manifest.SizeBytes,
                    ContentHash = manifest.Hash,
                    CreatedAt = DateTime.UtcNow,
                    ModifiedAt = DateTime.UtcNow
                };

                await _index.IndexAsync(newManifest, cancellationToken);
            }
        }

        private async Task RestoreConfigurationAsync(Snapshot snapshot, RestoreOptions options, CancellationToken cancellationToken)
        {
            var snapshotPath = Path.Combine(_context.RootPath, "snapshots", snapshot.SnapshotId);
            var sourceConfigPath = Path.Combine(snapshotPath, "Config");

            if (!Directory.Exists(sourceConfigPath))
                return;

            var targetConfigPath = Path.Combine(options.TargetPath ?? _context.RootPath, "Config");
            await CopyDirectoryAsync(sourceConfigPath, targetConfigPath, options.OverwriteExisting, cancellationToken);
        }

        private async Task RestoreMetadataAsync(Snapshot snapshot, RestoreOptions options, CancellationToken cancellationToken)
        {
            var snapshotPath = Path.Combine(_context.RootPath, "snapshots", snapshot.SnapshotId);
            var sourceMetadataPath = Path.Combine(snapshotPath, "Metadata");

            if (!Directory.Exists(sourceMetadataPath))
                return;

            var targetMetadataPath = Path.Combine(options.TargetPath ?? _context.RootPath, "Metadata");
            await CopyDirectoryAsync(sourceMetadataPath, targetMetadataPath, options.OverwriteExisting, cancellationToken);
        }

        private static async Task CopyDirectoryAsync(string sourceDir, string targetDir, bool overwrite, CancellationToken cancellationToken)
        {
            foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var relativePath = Path.GetRelativePath(sourceDir, file);
                var targetFile = Path.Combine(targetDir, relativePath);

                Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
                File.Copy(file, targetFile, overwrite);
            }

            await Task.CompletedTask;
        }

        #endregion

        #region Permission Validation

        private async Task<bool> ValidateRestorePermissionAsync(
            Snapshot snapshot,
            RestoreOptions options,
            ISecurityContext securityContext,
            CancellationToken cancellationToken)
        {
            if (_securityProvider == null)
                return true;

            // Check restore permission based on granularity
            var requiredPermission = snapshot.Granularity switch
            {
                RestoreGranularity.SingleFile => Permission.Read,
                RestoreGranularity.Compartment => Permission.Read,
                RestoreGranularity.Partition => Permission.Write,
                RestoreGranularity.StorageLayer => Permission.Write,
                RestoreGranularity.StoragePool => Permission.FullControl,
                RestoreGranularity.MultiplePools => Permission.FullControl,
                RestoreGranularity.CompleteInstance => Permission.FullControl,
                _ => Permission.FullControl
            };

            var result = await _securityProvider.CheckPermissionAsync(
                securityContext,
                $"restore:{snapshot.Granularity}",
                requiredPermission,
                cancellationToken);

            if (!result.IsAllowed)
            {
                _context.LogWarning($"[Restore] Permission denied for user {securityContext.UserId} to restore {snapshot.Granularity}");
            }

            return result.IsAllowed;
        }

        #endregion

        #region Helper Methods

        private static string CreateVersionedPath(string path)
        {
            var directory = Path.GetDirectoryName(path)!;
            var filename = Path.GetFileNameWithoutExtension(path);
            var extension = Path.GetExtension(path);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");

            return Path.Combine(directory, $"{filename}_v{timestamp}{extension}");
        }

        private static string CreateRenamedPath(string path)
        {
            var directory = Path.GetDirectoryName(path)!;
            var filename = Path.GetFileNameWithoutExtension(path);
            var extension = Path.GetExtension(path);

            int counter = 1;
            string newPath;
            do
            {
                newPath = Path.Combine(directory, $"{filename}({counter}){extension}");
                counter++;
            }
            while (File.Exists(newPath));

            return newPath;
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

    public class RestoreOptions
    {
        /// <summary>
        /// Target path for restore (null = restore to original location).
        /// </summary>
        public string? TargetPath { get; init; }

        /// <summary>
        /// Overwrite existing files.
        /// </summary>
        public bool OverwriteExisting { get; init; } = false;

        /// <summary>
        /// Conflict resolution strategy when files exist.
        /// </summary>
        public ConflictResolution ConflictResolution { get; init; } = ConflictResolution.Skip;

        /// <summary>
        /// Verify snapshot integrity before restore.
        /// </summary>
        public bool VerifyIntegrity { get; init; } = true;

        /// <summary>
        /// Restore original file timestamps.
        /// </summary>
        public bool RestoreTimestamps { get; init; } = true;

        /// <summary>
        /// Update metadata index after restore.
        /// </summary>
        public bool UpdateMetadataIndex { get; init; } = true;

        /// <summary>
        /// Selective restore: list of specific paths to restore (null = all).
        /// </summary>
        public List<string>? SelectivePaths { get; init; }
    }

    public enum ConflictResolution
    {
        /// <summary>
        /// Skip existing files.
        /// </summary>
        Skip,

        /// <summary>
        /// Overwrite existing files.
        /// </summary>
        Overwrite,

        /// <summary>
        /// Create versioned copy (filename_vYYYYMMDDHHMMSS.ext).
        /// </summary>
        CreateVersion,

        /// <summary>
        /// Rename to filename(1).ext, filename(2).ext, etc.
        /// </summary>
        Rename,

        /// <summary>
        /// Fail the restore operation.
        /// </summary>
        Fail
    }

    public class RestoreResult
    {
        public string? SnapshotId { get; init; }
        public RestoreGranularity Granularity { get; init; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public long DurationMs { get; set; }
        public int RestoredFiles { get; set; }
        public long RestoredBytes { get; set; }
        public int SkippedFiles { get; set; }
        public int FailedFiles { get; set; }
    }

    public class SnapshotBrowseResult
    {
        public bool Success { get; init; }
        public string? ErrorMessage { get; init; }
        public string? SnapshotId { get; init; }
        public RestoreGranularity Granularity { get; init; }
        public DateTime Timestamp { get; init; }
        public int TotalFiles { get; init; }
        public long TotalBytes { get; init; }
        public List<SnapshotFileInfo> Files { get; init; } = [];
    }

    public class SnapshotFileInfo
    {
        public required string Path { get; init; }
        public long SizeBytes { get; init; }
        public required string Hash { get; init; }
        public DateTime Timestamp { get; init; }
    }
}
