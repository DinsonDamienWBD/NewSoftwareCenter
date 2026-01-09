using DataWarehouse.SDK.Contracts;
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text.Json;

namespace DataWarehouse.Kernel.Backup
{
    /// <summary>
    /// Automated backup and restore system for DataWarehouse.
    /// Supports full, incremental, and differential backups with retention policies.
    /// </summary>
    public class BackupManager
    {
        private readonly IKernelContext _context;
        private readonly string _backupDirectory;
        private readonly ConcurrentDictionary<string, BackupJob> _activeJobs = new();
        private readonly ConcurrentDictionary<string, BackupMetadata> _backupRegistry = new();

        // Configuration
        private BackupPolicy _policy = new();
        private CancellationTokenSource? _schedulerCancellation;

        public BackupManager(IKernelContext context, string? backupDirectory = null)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _backupDirectory = backupDirectory ?? Path.Combine(context.RootPath, "backups");
            Directory.CreateDirectory(_backupDirectory);
        }

        /// <summary>
        /// Initialize backup manager with policy.
        /// </summary>
        public async Task InitializeAsync(BackupPolicy policy)
        {
            _policy = policy;
            _context.LogInfo($"[Backup] Initialized with policy: {policy.Type}, retention: {policy.RetentionDays} days");

            // Load backup registry
            await LoadBackupRegistryAsync();

            // Start scheduler if automated backups enabled
            if (policy.AutomatedBackups)
            {
                StartScheduler();
            }
        }

        /// <summary>
        /// Start automated backup scheduler.
        /// </summary>
        public void StartScheduler()
        {
            _schedulerCancellation = new CancellationTokenSource();
            _context.LogInfo($"[Backup] Started scheduler, interval: {_policy.BackupInterval}");

            _ = Task.Run(async () => await SchedulerLoopAsync(_schedulerCancellation.Token));
        }

        /// <summary>
        /// Stop automated backup scheduler.
        /// </summary>
        public void StopScheduler()
        {
            _schedulerCancellation?.Cancel();
            _context.LogInfo("[Backup] Stopped scheduler");
        }

        /// <summary>
        /// Create a backup.
        /// </summary>
        public async Task<BackupResult> CreateBackupAsync(
            BackupType type,
            string? description = null,
            CancellationToken cancellationToken = default)
        {
            var backupId = Guid.NewGuid().ToString();
            var timestamp = DateTime.UtcNow;

            _context.LogInfo($"[Backup] Starting {type} backup: {backupId}");

            var job = new BackupJob
            {
                BackupId = backupId,
                Type = type,
                Status = BackupStatus.InProgress,
                StartTime = timestamp
            };

            _activeJobs[backupId] = job;

            try
            {
                var backupPath = GetBackupPath(backupId, timestamp);
                Directory.CreateDirectory(backupPath);

                var metadata = new BackupMetadata
                {
                    BackupId = backupId,
                    Type = type,
                    Timestamp = timestamp,
                    Description = description ?? $"{type} backup",
                    Status = BackupStatus.InProgress
                };

                // Perform backup based on type
                switch (type)
                {
                    case BackupType.Full:
                        await CreateFullBackupAsync(backupPath, metadata, cancellationToken);
                        break;

                    case BackupType.Incremental:
                        await CreateIncrementalBackupAsync(backupPath, metadata, cancellationToken);
                        break;

                    case BackupType.Differential:
                        await CreateDifferentialBackupAsync(backupPath, metadata, cancellationToken);
                        break;

                    default:
                        throw new NotSupportedException($"Backup type {type} not supported");
                }

                // Compress if enabled
                if (_policy.CompressBackups)
                {
                    await CompressBackupAsync(backupPath, cancellationToken);
                    metadata.IsCompressed = true;
                }

                // Update metadata
                metadata.Status = BackupStatus.Completed;
                metadata.CompletionTime = DateTime.UtcNow;
                metadata.Duration = metadata.CompletionTime.Value - metadata.Timestamp;

                // Calculate backup size
                metadata.SizeBytes = CalculateBackupSize(backupPath);

                // Save metadata
                await SaveBackupMetadataAsync(backupPath, metadata);
                _backupRegistry[backupId] = metadata;

                // Update job
                job.Status = BackupStatus.Completed;
                job.EndTime = DateTime.UtcNow;

                _context.LogInfo($"[Backup] Completed {type} backup: {backupId}, size: {FormatBytes(metadata.SizeBytes)}");

                // Cleanup old backups based on retention policy
                await ApplyRetentionPolicyAsync();

                return new BackupResult
                {
                    BackupId = backupId,
                    Success = true,
                    Metadata = metadata
                };
            }
            catch (Exception ex)
            {
                _context.LogError($"[Backup] Failed to create {type} backup", ex);
                job.Status = BackupStatus.Failed;
                job.Error = ex.Message;

                return new BackupResult
                {
                    BackupId = backupId,
                    Success = false,
                    Error = ex.Message
                };
            }
            finally
            {
                _activeJobs.TryRemove(backupId, out _);
            }
        }

        /// <summary>
        /// Restore from backup.
        /// </summary>
        public async Task<RestoreResult> RestoreAsync(
            string backupId,
            string? targetPath = null,
            CancellationToken cancellationToken = default)
        {
            _context.LogInfo($"[Backup] Starting restore from backup: {backupId}");

            if (!_backupRegistry.TryGetValue(backupId, out var metadata))
            {
                return new RestoreResult
                {
                    Success = false,
                    Error = $"Backup {backupId} not found"
                };
            }

            try
            {
                var backupPath = GetBackupPath(backupId, metadata.Timestamp);
                targetPath ??= _context.RootPath;

                // Decompress if needed
                if (metadata.IsCompressed)
                {
                    await DecompressBackupAsync(backupPath, cancellationToken);
                }

                // Restore files
                await RestoreFilesAsync(backupPath, targetPath, metadata, cancellationToken);

                _context.LogInfo($"[Backup] Restore completed from backup: {backupId}");

                return new RestoreResult
                {
                    BackupId = backupId,
                    Success = true,
                    RestoredFiles = metadata.FileCount
                };
            }
            catch (Exception ex)
            {
                _context.LogError($"[Backup] Restore failed", ex);
                return new RestoreResult
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }

        /// <summary>
        /// List all available backups.
        /// </summary>
        public List<BackupMetadata> ListBackups(BackupType? type = null)
        {
            var backups = _backupRegistry.Values.AsEnumerable();

            if (type.HasValue)
            {
                backups = backups.Where(b => b.Type == type.Value);
            }

            return [.. backups.OrderByDescending(b => b.Timestamp)];
        }

        /// <summary>
        /// Delete a backup.
        /// </summary>
        public async Task<bool> DeleteBackupAsync(string backupId)
        {
            if (!_backupRegistry.TryGetValue(backupId, out var metadata))
                return false;

            try
            {
                var backupPath = GetBackupPath(backupId, metadata.Timestamp);
                if (Directory.Exists(backupPath))
                {
                    Directory.Delete(backupPath, recursive: true);
                }

                _backupRegistry.TryRemove(backupId, out _);
                _context.LogInfo($"[Backup] Deleted backup: {backupId}");

                await Task.CompletedTask;
                return true;
            }
            catch (Exception ex)
            {
                _context.LogError($"[Backup] Failed to delete backup {backupId}", ex);
                return false;
            }
        }

        private async Task CreateFullBackupAsync(
            string backupPath,
            BackupMetadata metadata,
            CancellationToken cancellationToken)
        {
            var dataPath = Path.Combine(_context.RootPath, "data");
            if (!Directory.Exists(dataPath))
            {
                Directory.CreateDirectory(dataPath);
            }

            var targetDataPath = Path.Combine(backupPath, "data");
            Directory.CreateDirectory(targetDataPath);

            // Copy all files
            await CopyDirectoryAsync(dataPath, targetDataPath, cancellationToken);

            metadata.FileCount = Directory.GetFiles(targetDataPath, "*", SearchOption.AllDirectories).Length;
            metadata.BaseBackupId = null; // Full backup has no base
        }

        private async Task CreateIncrementalBackupAsync(
            string backupPath,
            BackupMetadata metadata,
            CancellationToken cancellationToken)
        {
            // Find most recent backup (full or incremental)
            var baseBackup = _backupRegistry.Values
                .Where(b => b.Status == BackupStatus.Completed)
                .OrderByDescending(b => b.Timestamp)
                .FirstOrDefault();

            if (baseBackup == null)
            {
                // No base backup, create full backup instead
                _context.LogWarning("[Backup] No base backup found, creating full backup instead");
                await CreateFullBackupAsync(backupPath, metadata, cancellationToken);
                metadata.Type = BackupType.Full;
                return;
            }

            metadata.BaseBackupId = baseBackup.BackupId;

            // Copy only files modified since last backup
            var dataPath = Path.Combine(_context.RootPath, "data");
            var targetDataPath = Path.Combine(backupPath, "data");
            Directory.CreateDirectory(targetDataPath);

            await CopyModifiedFilesAsync(dataPath, targetDataPath, baseBackup.Timestamp, cancellationToken);

            metadata.FileCount = Directory.GetFiles(targetDataPath, "*", SearchOption.AllDirectories).Length;
        }

        private async Task CreateDifferentialBackupAsync(
            string backupPath,
            BackupMetadata metadata,
            CancellationToken cancellationToken)
        {
            // Find most recent full backup
            var baseBackup = _backupRegistry.Values
                .Where(b => b.Type == BackupType.Full && b.Status == BackupStatus.Completed)
                .OrderByDescending(b => b.Timestamp)
                .FirstOrDefault();

            if (baseBackup == null)
            {
                _context.LogWarning("[Backup] No full backup found, creating full backup instead");
                await CreateFullBackupAsync(backupPath, metadata, cancellationToken);
                metadata.Type = BackupType.Full;
                return;
            }

            metadata.BaseBackupId = baseBackup.BackupId;

            // Copy files modified since last full backup
            var dataPath = Path.Combine(_context.RootPath, "data");
            var targetDataPath = Path.Combine(backupPath, "data");
            Directory.CreateDirectory(targetDataPath);

            await CopyModifiedFilesAsync(dataPath, targetDataPath, baseBackup.Timestamp, cancellationToken);

            metadata.FileCount = Directory.GetFiles(targetDataPath, "*", SearchOption.AllDirectories).Length;
        }

        private static async Task CopyDirectoryAsync(string sourceDir, string targetDir, CancellationToken cancellationToken)
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

        private static async Task CopyModifiedFilesAsync(
            string sourceDir,
            string targetDir,
            DateTime since,
            CancellationToken cancellationToken)
        {
            foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var lastWrite = File.GetLastWriteTimeUtc(file);
                if (lastWrite <= since)
                    continue;

                var relativePath = Path.GetRelativePath(sourceDir, file);
                var targetFile = Path.Combine(targetDir, relativePath);

                Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
                File.Copy(file, targetFile, overwrite: true);
            }

            await Task.CompletedTask;
        }

        private static async Task CompressBackupAsync(string backupPath, CancellationToken cancellationToken)
        {
            var zipPath = $"{backupPath}.zip";
            ZipFile.CreateFromDirectory(backupPath, zipPath, System.IO.Compression.CompressionLevel.Optimal, includeBaseDirectory: false);

            // Delete uncompressed directory
            Directory.Delete(backupPath, recursive: true);

            await Task.CompletedTask;
        }

        private static async Task DecompressBackupAsync(string backupPath, CancellationToken cancellationToken)
        {
            var zipPath = $"{backupPath}.zip";
            if (File.Exists(zipPath))
            {
                ZipFile.ExtractToDirectory(zipPath, backupPath, overwriteFiles: true);
            }

            await Task.CompletedTask;
        }

        private async Task RestoreFilesAsync(
            string backupPath,
            string targetPath,
            BackupMetadata metadata,
            CancellationToken cancellationToken)
        {
            // If incremental/differential, need to restore base backup first
            if (metadata.BaseBackupId != null)
            {
                if (_backupRegistry.TryGetValue(metadata.BaseBackupId, out var baseMetadata))
                {
                    var basePath = GetBackupPath(metadata.BaseBackupId, baseMetadata.Timestamp);
                    await RestoreFilesAsync(basePath, targetPath, baseMetadata, cancellationToken);
                }
            }

            // Restore this backup's files
            var sourceDataPath = Path.Combine(backupPath, "data");
            var targetDataPath = Path.Combine(targetPath, "data");

            if (Directory.Exists(sourceDataPath))
            {
                await CopyDirectoryAsync(sourceDataPath, targetDataPath, cancellationToken);
            }
        }

        private async Task ApplyRetentionPolicyAsync()
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-_policy.RetentionDays);
            var oldBackups = _backupRegistry.Values
                .Where(b => b.Timestamp < cutoffDate)
                .ToList();

            foreach (var backup in oldBackups)
            {
                _context.LogInfo($"[Backup] Deleting old backup: {backup.BackupId} ({backup.Timestamp:yyyy-MM-dd})");
                await DeleteBackupAsync(backup.BackupId);
            }
        }

        private async Task SchedulerLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_policy.BackupInterval, cancellationToken);
                    await CreateBackupAsync(_policy.Type, "Automated backup", cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _context.LogError("[Backup] Scheduler error", ex);
                }
            }
        }

        private string GetBackupPath(string backupId, DateTime timestamp)
        {
            var folderName = $"{timestamp:yyyy-MM-dd_HHmmss}_{backupId[..8]}";
            return Path.Combine(_backupDirectory, folderName);
        }

        private static long CalculateBackupSize(string path)
        {
            if (File.Exists($"{path}.zip"))
            {
                return new FileInfo($"{path}.zip").Length;
            }

            if (Directory.Exists(path))
            {
                return Directory.GetFiles(path, "*", SearchOption.AllDirectories)
                    .Sum(f => new FileInfo(f).Length);
            }

            return 0;
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

        private static async Task SaveBackupMetadataAsync(string backupPath, BackupMetadata metadata)
        {
            var metadataFile = Path.Combine(backupPath, "metadata.json");
            var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(metadataFile, json);
        }

        private async Task LoadBackupRegistryAsync()
        {
            if (!Directory.Exists(_backupDirectory))
                return;

            foreach (var backupDir in Directory.GetDirectories(_backupDirectory))
            {
                var metadataFile = Path.Combine(backupDir, "metadata.json");
                if (File.Exists(metadataFile))
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(metadataFile);
                        var metadata = JsonSerializer.Deserialize<BackupMetadata>(json);
                        if (metadata != null)
                        {
                            _backupRegistry[metadata.BackupId] = metadata;
                        }
                    }
                    catch (Exception ex)
                    {
                        _context.LogWarning($"[Backup] Failed to load backup metadata: {ex.Message}");
                    }
                }
            }

            _context.LogInfo($"[Backup] Loaded {_backupRegistry.Count} backups from registry");
        }
    }

    public class BackupPolicy
    {
        public BackupType Type { get; init; } = BackupType.Incremental;
        public bool AutomatedBackups { get; init; } = true;
        public TimeSpan BackupInterval { get; init; } = TimeSpan.FromHours(24);
        public int RetentionDays { get; init; } = 30;
        public bool CompressBackups { get; init; } = true;
    }

    public enum BackupType
    {
        Full,
        Incremental,
        Differential
    }

    public enum BackupStatus
    {
        InProgress,
        Completed,
        Failed
    }

    public class BackupJob
    {
        public required string BackupId { get; init; }
        public BackupType Type { get; init; }
        public BackupStatus Status { get; set; }
        public DateTime StartTime { get; init; }
        public DateTime? EndTime { get; set; }
        public string? Error { get; set; }
    }

    public class BackupMetadata
    {
        public required string BackupId { get; init; }
        public BackupType Type { get; set; }
        public DateTime Timestamp { get; init; }
        public required string Description { get; init; }
        public BackupStatus Status { get; set; }
        public DateTime? CompletionTime { get; set; }
        public TimeSpan Duration { get; set; }
        public long SizeBytes { get; set; }
        public int FileCount { get; set; }
        public bool IsCompressed { get; set; }
        public string? BaseBackupId { get; set; }
    }

    public class BackupResult
    {
        public required string BackupId { get; init; }
        public bool Success { get; init; }
        public BackupMetadata? Metadata { get; init; }
        public string? Error { get; init; }
    }

    public class RestoreResult
    {
        public string? BackupId { get; init; }
        public bool Success { get; init; }
        public int RestoredFiles { get; init; }
        public string? Error { get; init; }
    }
}
