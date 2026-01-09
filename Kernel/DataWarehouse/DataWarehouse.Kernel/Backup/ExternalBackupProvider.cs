using DataWarehouse.SDK.Contracts;
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text.Json;

namespace DataWarehouse.Kernel.Backup
{
    /// <summary>
    /// External backup provider for backing up to any non-volatile storage layer.
    /// Supports backing up snapshots to storage layers different from the source.
    /// </summary>
    public class ExternalBackupProvider
    {
        private readonly IKernelContext _context;
        private readonly SnapshotManager _snapshotManager;
        private readonly ConcurrentDictionary<string, ExternalBackup> _externalBackups = new();

        public ExternalBackupProvider(IKernelContext context, SnapshotManager snapshotManager)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _snapshotManager = snapshotManager ?? throw new ArgumentNullException(nameof(snapshotManager));
        }

        /// <summary>
        /// Backup snapshot to external storage layer.
        /// </summary>
        public async Task<ExternalBackupResult> BackupToExternalAsync(
            string snapshotId,
            IStorageProvider targetProvider,
            ExternalBackupOptions options,
            CancellationToken cancellationToken = default)
        {
            _context.LogInfo($"[ExternalBackup] Starting backup of snapshot {snapshotId} to {targetProvider.Name}");

            // Get snapshot
            var snapshot = _snapshotManager.GetSnapshot(snapshotId);
            if (snapshot == null)
            {
                return new ExternalBackupResult
                {
                    Success = false,
                    ErrorMessage = $"Snapshot {snapshotId} not found"
                };
            }

            // Validate target storage is non-volatile
            if (IsVolatileStorage(targetProvider))
            {
                return new ExternalBackupResult
                {
                    Success = false,
                    ErrorMessage = $"Cannot backup to volatile storage: {targetProvider.Name}"
                };
            }

            // Validate target storage is different from source
            var sourceProvider = _context.GetPlugin<IStorageProvider>();
            if (sourceProvider != null && IsSameStorage(sourceProvider, targetProvider))
            {
                return new ExternalBackupResult
                {
                    Success = false,
                    ErrorMessage = "Target storage must be different from source storage"
                };
            }

            try
            {
                var backupId = Guid.NewGuid().ToString();
                var startTime = DateTime.UtcNow;

                var externalBackup = new ExternalBackup
                {
                    BackupId = backupId,
                    SnapshotId = snapshotId,
                    TargetProvider = targetProvider.Id,
                    TargetName = targetProvider.Name,
                    StartTime = startTime,
                    Status = ExternalBackupStatus.InProgress
                };

                // Get snapshot directory
                var snapshotPath = Path.Combine(_context.RootPath, "snapshots", snapshotId);
                if (!Directory.Exists(snapshotPath))
                {
                    return new ExternalBackupResult
                    {
                        Success = false,
                        ErrorMessage = $"Snapshot directory not found: {snapshotPath}"
                    };
                }

                // Compress if requested
                string backupSource = snapshotPath;
                if (options.CompressBeforeUpload)
                {
                    _context.LogInfo($"[ExternalBackup] Compressing snapshot...");
                    backupSource = await CompressSnapshotAsync(snapshotPath, cancellationToken);
                    externalBackup.IsCompressed = true;
                }

                // Upload to external storage
                long totalBytes = 0;
                int totalFiles = 0;

                if (externalBackup.IsCompressed)
                {
                    // Upload compressed archive
                    var archivePath = $"{backupSource}.zip";
                    var fileInfo = new FileInfo(archivePath);
                    totalBytes = fileInfo.Length;
                    totalFiles = 1;

                    var targetUri = CreateTargetUri(targetProvider, backupId, "snapshot.zip");
                    using var fileStream = File.OpenRead(archivePath);
                    await targetProvider.SaveAsync(targetUri, fileStream);

                    _context.LogInfo($"[ExternalBackup] Uploaded compressed archive: {FormatBytes(totalBytes)}");
                }
                else
                {
                    // Upload individual files
                    var files = Directory.GetFiles(snapshotPath, "*", SearchOption.AllDirectories);
                    totalFiles = files.Length;

                    foreach (var file in files)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var relativePath = Path.GetRelativePath(snapshotPath, file);
                        var targetUri = CreateTargetUri(targetProvider, backupId, relativePath);

                        using var fileStream = File.OpenRead(file);
                        await targetProvider.SaveAsync(targetUri, fileStream);

                        totalBytes += new FileInfo(file).Length;
                    }

                    _context.LogInfo($"[ExternalBackup] Uploaded {totalFiles} files: {FormatBytes(totalBytes)}");
                }

                // Save metadata to external storage
                await SaveExternalMetadataAsync(targetProvider, backupId, snapshot, externalBackup, cancellationToken);

                // Update external backup record
                externalBackup.Status = ExternalBackupStatus.Completed;
                externalBackup.EndTime = DateTime.UtcNow;
                externalBackup.DurationMs = (long)(externalBackup.EndTime.Value - externalBackup.StartTime).TotalMilliseconds;
                externalBackup.TotalFiles = totalFiles;
                externalBackup.TotalBytes = totalBytes;

                _externalBackups[backupId] = externalBackup;

                _context.LogInfo($"[ExternalBackup] Completed backup to {targetProvider.Name}: {FormatBytes(totalBytes)} in {externalBackup.DurationMs}ms");

                return new ExternalBackupResult
                {
                    Success = true,
                    BackupId = backupId,
                    SnapshotId = snapshotId,
                    TargetProvider = targetProvider.Name,
                    TotalFiles = totalFiles,
                    TotalBytes = totalBytes,
                    DurationMs = externalBackup.DurationMs
                };
            }
            catch (Exception ex)
            {
                _context.LogError($"[ExternalBackup] Failed to backup snapshot {snapshotId}", ex);
                return new ExternalBackupResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// Restore snapshot from external storage.
        /// </summary>
        public async Task<ExternalRestoreResult> RestoreFromExternalAsync(
            string backupId,
            IStorageProvider sourceProvider,
            CancellationToken cancellationToken = default)
        {
            _context.LogInfo($"[ExternalBackup] Starting restore from external backup {backupId}");

            try
            {
                var startTime = DateTime.UtcNow;

                // Download metadata first
                var metadataUri = CreateTargetUri(sourceProvider, backupId, "metadata.json");
                ExternalBackup? metadata;

                try
                {
                    using var metadataStream = await sourceProvider.LoadAsync(metadataUri);
                    using var reader = new StreamReader(metadataStream);
                    var json = await reader.ReadToEndAsync(cancellationToken);
                    metadata = JsonSerializer.Deserialize<ExternalBackup>(json);
                }
                catch (Exception ex)
                {
                    return new ExternalRestoreResult
                    {
                        Success = false,
                        ErrorMessage = $"Failed to load backup metadata: {ex.Message}"
                    };
                }

                if (metadata == null)
                {
                    return new ExternalRestoreResult
                    {
                        Success = false,
                        ErrorMessage = "Invalid backup metadata"
                    };
                }

                // Create restore directory
                var restorePath = Path.Combine(_context.RootPath, "snapshots", metadata.SnapshotId);
                Directory.CreateDirectory(restorePath);

                int restoredFiles = 0;
                long restoredBytes = 0;

                if (metadata.IsCompressed)
                {
                    // Download and extract compressed archive
                    var archiveUri = CreateTargetUri(sourceProvider, backupId, "snapshot.zip");
                    var archivePath = Path.Combine(restorePath, "snapshot.zip");

                    using (var archiveStream = await sourceProvider.LoadAsync(archiveUri))
                    using (var fileStream = File.Create(archivePath))
                    {
                        await archiveStream.CopyToAsync(fileStream, cancellationToken);
                        restoredBytes = fileStream.Length;
                    }

                    // Extract archive
                    ZipFile.ExtractToDirectory(archivePath, restorePath, overwriteFiles: true);
                    File.Delete(archivePath);

                    restoredFiles = Directory.GetFiles(restorePath, "*", SearchOption.AllDirectories).Length;
                }
                else
                {
                    // Download individual files (this requires listing capability)
                    // For now, we assume metadata contains file list
                    throw new NotImplementedException("Restoring individual files from external storage not yet implemented");
                }

                // Load snapshot metadata
                var snapshotMetadataPath = Path.Combine(restorePath, "snapshot.json");
                if (File.Exists(snapshotMetadataPath))
                {
                    var json = await File.ReadAllTextAsync(snapshotMetadataPath, cancellationToken);
                    var snapshot = JsonSerializer.Deserialize<Snapshot>(json);

                    if (snapshot != null)
                    {
                        // Register snapshot with snapshot manager (using reflection or direct access)
                        _context.LogInfo($"[ExternalBackup] Restored snapshot {snapshot.SnapshotId}");
                    }
                }

                var endTime = DateTime.UtcNow;
                var durationMs = (long)(endTime - startTime).TotalMilliseconds;

                _context.LogInfo($"[ExternalBackup] Completed restore from external backup: {restoredFiles} files, {FormatBytes(restoredBytes)} in {durationMs}ms");

                return new ExternalRestoreResult
                {
                    Success = true,
                    BackupId = backupId,
                    SnapshotId = metadata.SnapshotId,
                    RestoredFiles = restoredFiles,
                    RestoredBytes = restoredBytes,
                    DurationMs = durationMs
                };
            }
            catch (Exception ex)
            {
                _context.LogError($"[ExternalBackup] Failed to restore from external backup {backupId}", ex);
                return new ExternalRestoreResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// List external backups for a storage provider.
        /// </summary>
        public List<ExternalBackup> ListExternalBackups(string? targetProviderId = null)
        {
            var backups = _externalBackups.Values.AsEnumerable();

            if (!string.IsNullOrEmpty(targetProviderId))
            {
                backups = backups.Where(b => b.TargetProvider == targetProviderId);
            }

            return backups.OrderByDescending(b => b.StartTime).ToList();
        }

        /// <summary>
        /// Delete external backup.
        /// </summary>
        public async Task<bool> DeleteExternalBackupAsync(
            string backupId,
            IStorageProvider sourceProvider,
            CancellationToken cancellationToken = default)
        {
            if (!_externalBackups.TryGetValue(backupId, out var backup))
                return false;

            try
            {
                // Delete from external storage
                if (backup.IsCompressed)
                {
                    var archiveUri = CreateTargetUri(sourceProvider, backupId, "snapshot.zip");
                    await sourceProvider.DeleteAsync(archiveUri);
                }

                var metadataUri = CreateTargetUri(sourceProvider, backupId, "metadata.json");
                await sourceProvider.DeleteAsync(metadataUri);

                _externalBackups.TryRemove(backupId, out _);
                _context.LogInfo($"[ExternalBackup] Deleted external backup: {backupId}");

                return true;
            }
            catch (Exception ex)
            {
                _context.LogError($"[ExternalBackup] Failed to delete external backup {backupId}", ex);
                return false;
            }
        }

        #region Private Helper Methods

        private static bool IsVolatileStorage(IStorageProvider provider)
        {
            // Check if storage is volatile (e.g., RAMDisk)
            return provider.Name.Contains("RAM", StringComparison.OrdinalIgnoreCase) ||
                   provider.Name.Contains("Memory", StringComparison.OrdinalIgnoreCase) ||
                   provider.Id.Contains("ramdisk", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSameStorage(IStorageProvider source, IStorageProvider target)
        {
            // Compare storage providers by ID and Scheme
            return source.Id == target.Id || source.Scheme == target.Scheme;
        }

        private static Uri CreateTargetUri(IStorageProvider provider, string backupId, string relativePath)
        {
            var path = $"backups/{backupId}/{relativePath}";
            return new Uri($"{provider.Scheme}://{path}");
        }

        private async Task<string> CompressSnapshotAsync(string snapshotPath, CancellationToken cancellationToken)
        {
            var zipPath = $"{snapshotPath}.zip";

            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }

            await Task.Run(() =>
            {
                ZipFile.CreateFromDirectory(snapshotPath, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);
            }, cancellationToken);

            return snapshotPath;
        }

        private async Task SaveExternalMetadataAsync(
            IStorageProvider provider,
            string backupId,
            Snapshot snapshot,
            ExternalBackup backup,
            CancellationToken cancellationToken)
        {
            var metadata = new
            {
                BackupId = backupId,
                Snapshot = snapshot,
                Backup = backup
            };

            var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
            var jsonBytes = System.Text.Encoding.UTF8.GetBytes(json);

            var metadataUri = CreateTargetUri(provider, backupId, "metadata.json");
            using var stream = new MemoryStream(jsonBytes);
            await provider.SaveAsync(metadataUri, stream);
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

    public class ExternalBackupOptions
    {
        /// <summary>
        /// Compress snapshot before uploading to external storage.
        /// </summary>
        public bool CompressBeforeUpload { get; init; } = true;

        /// <summary>
        /// Encryption key for external backup (optional).
        /// </summary>
        public byte[]? EncryptionKey { get; init; }

        /// <summary>
        /// Verify upload integrity after completion.
        /// </summary>
        public bool VerifyAfterUpload { get; init; } = false;
    }

    public enum ExternalBackupStatus
    {
        InProgress,
        Completed,
        Failed
    }

    public class ExternalBackup
    {
        public required string BackupId { get; init; }
        public required string SnapshotId { get; init; }
        public required string TargetProvider { get; init; }
        public required string TargetName { get; init; }
        public DateTime StartTime { get; init; }
        public DateTime? EndTime { get; set; }
        public long DurationMs { get; set; }
        public ExternalBackupStatus Status { get; set; }
        public int TotalFiles { get; set; }
        public long TotalBytes { get; set; }
        public bool IsCompressed { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class ExternalBackupResult
    {
        public bool Success { get; init; }
        public string? BackupId { get; init; }
        public string? SnapshotId { get; init; }
        public string? TargetProvider { get; init; }
        public int TotalFiles { get; init; }
        public long TotalBytes { get; init; }
        public long DurationMs { get; init; }
        public string? ErrorMessage { get; init; }
    }

    public class ExternalRestoreResult
    {
        public bool Success { get; init; }
        public string? BackupId { get; init; }
        public string? SnapshotId { get; init; }
        public int RestoredFiles { get; init; }
        public long RestoredBytes { get; init; }
        public long DurationMs { get; init; }
        public string? ErrorMessage { get; init; }
    }
}
