using DataWarehouse.SDK.Contracts;
using System.Collections.Concurrent;

namespace DataWarehouse.Kernel.Backup
{
    /// <summary>
    /// Time Machine-style backup browser service.
    /// Provides UI-friendly interface for exploring snapshots, comparing versions, and browsing history.
    /// </summary>
    public class BackupBrowserService
    {
        private readonly IKernelContext _context;
        private readonly SnapshotManager _snapshotManager;
        private readonly RestoreEngine _restoreEngine;
        private readonly ConcurrentDictionary<string, BrowseSession> _activeSessions = new();

        public BackupBrowserService(
            IKernelContext context,
            SnapshotManager snapshotManager,
            RestoreEngine restoreEngine)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _snapshotManager = snapshotManager ?? throw new ArgumentNullException(nameof(snapshotManager));
            _restoreEngine = restoreEngine ?? throw new ArgumentNullException(nameof(restoreEngine));
        }

        /// <summary>
        /// Start a new browse session for exploring snapshots.
        /// </summary>
        public BrowseSession StartBrowseSession(RestoreGranularity? granularity = null)
        {
            var sessionId = Guid.NewGuid().ToString();
            var session = new BrowseSession
            {
                SessionId = sessionId,
                StartTime = DateTime.UtcNow,
                FilterGranularity = granularity
            };

            _activeSessions[sessionId] = session;
            _context.LogInfo($"[Browser] Started browse session: {sessionId}");

            return session;
        }

        /// <summary>
        /// End a browse session.
        /// </summary>
        public void EndBrowseSession(string sessionId)
        {
            if (_activeSessions.TryRemove(sessionId, out _))
            {
                _context.LogInfo($"[Browser] Ended browse session: {sessionId}");
            }
        }

        /// <summary>
        /// Get timeline view of snapshots (Time Machine style).
        /// </summary>
        public TimelineView GetTimeline(
            DateTime? startDate = null,
            DateTime? endDate = null,
            RestoreGranularity? granularity = null)
        {
            var snapshots = _snapshotManager.ListSnapshots(
                granularity,
                since: startDate,
                until: endDate);

            var timeline = new TimelineView
            {
                StartDate = startDate ?? (snapshots.Any() ? snapshots.Min(s => s.Timestamp) : DateTime.UtcNow.AddDays(-30)),
                EndDate = endDate ?? DateTime.UtcNow,
                TotalSnapshots = snapshots.Count,
                Snapshots = snapshots.Select(s => new TimelineEntry
                {
                    SnapshotId = s.SnapshotId,
                    Timestamp = s.Timestamp,
                    Granularity = s.Granularity,
                    Description = s.Description,
                    TotalFiles = s.TotalFiles,
                    TotalBytes = s.TotalBytes,
                    IsProtected = s.IsProtected
                }).ToList()
            };

            // Group by time periods for Time Machine view
            timeline.Hourly = GroupByPeriod(snapshots, TimeSpan.FromHours(1)).ToList();
            timeline.Daily = GroupByPeriod(snapshots, TimeSpan.FromDays(1)).ToList();
            timeline.Weekly = GroupByPeriod(snapshots, TimeSpan.FromDays(7)).ToList();
            timeline.Monthly = GroupByPeriod(snapshots, TimeSpan.FromDays(30)).ToList();

            return timeline;
        }

        /// <summary>
        /// Browse snapshot contents at a specific path.
        /// </summary>
        public async Task<DirectoryBrowseResult> BrowseDirectoryAsync(
            string snapshotId,
            string? path = null,
            CancellationToken cancellationToken = default)
        {
            var snapshot = _snapshotManager.GetSnapshot(snapshotId);
            if (snapshot == null)
            {
                return new DirectoryBrowseResult
                {
                    Success = false,
                    ErrorMessage = $"Snapshot {snapshotId} not found"
                };
            }

            path = path?.Trim('/') ?? "";
            var manifests = snapshot.Manifests;

            // Filter by current directory
            var filesInDirectory = manifests
                .Where(m =>
                {
                    var relativePath = m.RelativePath.TrimStart('/');
                    if (string.IsNullOrEmpty(path))
                        return !relativePath.Contains('/');

                    return relativePath.StartsWith(path + "/", StringComparison.OrdinalIgnoreCase) &&
                           relativePath[(path.Length + 1)..].IndexOf('/') == -1;
                })
                .Select(m => new FileEntry
                {
                    Path = m.RelativePath,
                    Name = Path.GetFileName(m.RelativePath),
                    SizeBytes = m.SizeBytes,
                    Hash = m.Hash,
                    Timestamp = m.Timestamp,
                    IsDirectory = false
                })
                .ToList();

            // Get subdirectories
            var subdirectories = manifests
                .Where(m =>
                {
                    var relativePath = m.RelativePath.TrimStart('/');
                    if (string.IsNullOrEmpty(path))
                        return relativePath.Contains('/');

                    return relativePath.StartsWith(path + "/", StringComparison.OrdinalIgnoreCase);
                })
                .Select(m =>
                {
                    var relativePath = m.RelativePath.TrimStart('/');
                    var remaining = string.IsNullOrEmpty(path)
                        ? relativePath
                        : relativePath[(path.Length + 1)..];

                    var slashIndex = remaining.IndexOf('/');
                    return slashIndex > 0 ? remaining[..slashIndex] : null;
                })
                .Where(d => d != null)
                .Distinct()
                .Select(d => new FileEntry
                {
                    Path = string.IsNullOrEmpty(path) ? d! : $"{path}/{d}",
                    Name = d!,
                    IsDirectory = true
                })
                .ToList();

            return new DirectoryBrowseResult
            {
                Success = true,
                SnapshotId = snapshotId,
                Path = path,
                Files = filesInDirectory,
                Directories = subdirectories,
                TotalFiles = filesInDirectory.Count,
                TotalDirectories = subdirectories.Count
            };
        }

        /// <summary>
        /// Compare two snapshots and show differences.
        /// </summary>
        public SnapshotComparisonResult CompareSnapshots(string snapshotId1, string snapshotId2)
        {
            var snapshot1 = _snapshotManager.GetSnapshot(snapshotId1);
            var snapshot2 = _snapshotManager.GetSnapshot(snapshotId2);

            if (snapshot1 == null || snapshot2 == null)
            {
                return new SnapshotComparisonResult
                {
                    Success = false,
                    ErrorMessage = "One or both snapshots not found"
                };
            }

            var manifests1 = snapshot1.Manifests.ToDictionary(m => m.RelativePath);
            var manifests2 = snapshot2.Manifests.ToDictionary(m => m.RelativePath);

            var addedFiles = manifests2.Keys.Except(manifests1.Keys)
                .Select(path => new FileDifference
                {
                    Path = path,
                    ChangeType = ChangeType.Added,
                    NewSize = manifests2[path].SizeBytes,
                    NewHash = manifests2[path].Hash,
                    NewTimestamp = manifests2[path].Timestamp
                })
                .ToList();

            var removedFiles = manifests1.Keys.Except(manifests2.Keys)
                .Select(path => new FileDifference
                {
                    Path = path,
                    ChangeType = ChangeType.Removed,
                    OldSize = manifests1[path].SizeBytes,
                    OldHash = manifests1[path].Hash,
                    OldTimestamp = manifests1[path].Timestamp
                })
                .ToList();

            var modifiedFiles = manifests1.Keys.Intersect(manifests2.Keys)
                .Where(path => manifests1[path].Hash != manifests2[path].Hash)
                .Select(path => new FileDifference
                {
                    Path = path,
                    ChangeType = ChangeType.Modified,
                    OldSize = manifests1[path].SizeBytes,
                    OldHash = manifests1[path].Hash,
                    OldTimestamp = manifests1[path].Timestamp,
                    NewSize = manifests2[path].SizeBytes,
                    NewHash = manifests2[path].Hash,
                    NewTimestamp = manifests2[path].Timestamp
                })
                .ToList();

            return new SnapshotComparisonResult
            {
                Success = true,
                Snapshot1Id = snapshotId1,
                Snapshot2Id = snapshotId2,
                Snapshot1Timestamp = snapshot1.Timestamp,
                Snapshot2Timestamp = snapshot2.Timestamp,
                AddedFiles = addedFiles,
                RemovedFiles = removedFiles,
                ModifiedFiles = modifiedFiles,
                TotalDifferences = addedFiles.Count + removedFiles.Count + modifiedFiles.Count
            };
        }

        /// <summary>
        /// Get version history for a specific file across snapshots.
        /// </summary>
        public FileHistoryResult GetFileHistory(
            string filePath,
            DateTime? since = null,
            DateTime? until = null)
        {
            var snapshots = _snapshotManager.ListSnapshots(since: since, until: until);

            var versions = new List<FileVersion>();

            foreach (var snapshot in snapshots)
            {
                var manifest = snapshot.Manifests.FirstOrDefault(m =>
                    m.RelativePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));

                if (manifest != null)
                {
                    versions.Add(new FileVersion
                    {
                        SnapshotId = snapshot.SnapshotId,
                        SnapshotTimestamp = snapshot.Timestamp,
                        FileHash = manifest.Hash,
                        FileSize = manifest.SizeBytes,
                        FileTimestamp = manifest.Timestamp
                    });
                }
            }

            return new FileHistoryResult
            {
                FilePath = filePath,
                TotalVersions = versions.Count,
                Versions = versions.OrderByDescending(v => v.SnapshotTimestamp).ToList()
            };
        }

        /// <summary>
        /// Search for files across all snapshots.
        /// </summary>
        public FileSearchResult SearchFiles(
            string searchPattern,
            DateTime? since = null,
            DateTime? until = null)
        {
            var snapshots = _snapshotManager.ListSnapshots(since: since, until: until);
            var results = new List<FileSearchMatch>();

            foreach (var snapshot in snapshots)
            {
                var matches = snapshot.Manifests
                    .Where(m => m.RelativePath.Contains(searchPattern, StringComparison.OrdinalIgnoreCase))
                    .Select(m => new FileSearchMatch
                    {
                        SnapshotId = snapshot.SnapshotId,
                        SnapshotTimestamp = snapshot.Timestamp,
                        FilePath = m.RelativePath,
                        FileSize = m.SizeBytes,
                        FileHash = m.Hash
                    });

                results.AddRange(matches);
            }

            return new FileSearchResult
            {
                SearchPattern = searchPattern,
                TotalMatches = results.Count,
                Matches = results.OrderByDescending(r => r.SnapshotTimestamp).ToList()
            };
        }

        /// <summary>
        /// Get storage usage statistics across snapshots.
        /// </summary>
        public StorageStatistics GetStorageStatistics(
            DateTime? since = null,
            DateTime? until = null)
        {
            var snapshots = _snapshotManager.ListSnapshots(since: since, until: until);

            var stats = new StorageStatistics
            {
                TotalSnapshots = snapshots.Count,
                TotalFiles = snapshots.Sum(s => s.TotalFiles),
                TotalBytes = snapshots.Sum(s => s.TotalBytes),
                EarliestSnapshot = snapshots.Any() ? snapshots.Min(s => s.Timestamp) : (DateTime?)null,
                LatestSnapshot = snapshots.Any() ? snapshots.Max(s => s.Timestamp) : (DateTime?)null
            };

            // Group by granularity
            stats.ByGranularity = snapshots
                .GroupBy(s => s.Granularity)
                .Select(g => new GranularityStats
                {
                    Granularity = g.Key,
                    Count = g.Count(),
                    TotalBytes = g.Sum(s => s.TotalBytes)
                })
                .OrderBy(g => g.Granularity)
                .ToList();

            // Calculate growth over time
            stats.GrowthTrend = CalculateGrowthTrend(snapshots);

            return stats;
        }

        #region Private Helper Methods

        private IEnumerable<TimelineGroup> GroupByPeriod(List<Snapshot> snapshots, TimeSpan period)
        {
            return snapshots
                .GroupBy(s =>
                {
                    var ticks = s.Timestamp.Ticks / period.Ticks;
                    return new DateTime(ticks * period.Ticks);
                })
                .Select(g => new TimelineGroup
                {
                    PeriodStart = g.Key,
                    PeriodEnd = g.Key.Add(period),
                    SnapshotCount = g.Count(),
                    TotalBytes = g.Sum(s => s.TotalBytes),
                    Snapshots = g.Select(s => s.SnapshotId).ToList()
                });
        }

        private List<GrowthDataPoint> CalculateGrowthTrend(List<Snapshot> snapshots)
        {
            if (!snapshots.Any())
                return new List<GrowthDataPoint>();

            var ordered = snapshots.OrderBy(s => s.Timestamp).ToList();
            var dataPoints = new List<GrowthDataPoint>();
            long cumulativeSize = 0;

            foreach (var snapshot in ordered)
            {
                cumulativeSize += snapshot.TotalBytes;
                dataPoints.Add(new GrowthDataPoint
                {
                    Timestamp = snapshot.Timestamp,
                    CumulativeBytes = cumulativeSize,
                    SnapshotBytes = snapshot.TotalBytes
                });
            }

            return dataPoints;
        }

        #endregion
    }

    #region Browse Session and Results

    public class BrowseSession
    {
        public required string SessionId { get; init; }
        public DateTime StartTime { get; init; }
        public RestoreGranularity? FilterGranularity { get; init; }
    }

    public class TimelineView
    {
        public DateTime StartDate { get; init; }
        public DateTime EndDate { get; init; }
        public int TotalSnapshots { get; init; }
        public List<TimelineEntry> Snapshots { get; init; } = new();
        public List<TimelineGroup> Hourly { get; set; } = new();
        public List<TimelineGroup> Daily { get; set; } = new();
        public List<TimelineGroup> Weekly { get; set; } = new();
        public List<TimelineGroup> Monthly { get; set; } = new();
    }

    public class TimelineEntry
    {
        public required string SnapshotId { get; init; }
        public DateTime Timestamp { get; init; }
        public RestoreGranularity Granularity { get; init; }
        public required string Description { get; init; }
        public int TotalFiles { get; init; }
        public long TotalBytes { get; init; }
        public bool IsProtected { get; init; }
    }

    public class TimelineGroup
    {
        public DateTime PeriodStart { get; init; }
        public DateTime PeriodEnd { get; init; }
        public int SnapshotCount { get; init; }
        public long TotalBytes { get; init; }
        public List<string> Snapshots { get; init; } = new();
    }

    public class DirectoryBrowseResult
    {
        public bool Success { get; init; }
        public string? ErrorMessage { get; init; }
        public string? SnapshotId { get; init; }
        public string? Path { get; init; }
        public List<FileEntry> Files { get; init; } = new();
        public List<FileEntry> Directories { get; init; } = new();
        public int TotalFiles { get; init; }
        public int TotalDirectories { get; init; }
    }

    public class FileEntry
    {
        public required string Path { get; init; }
        public required string Name { get; init; }
        public long SizeBytes { get; init; }
        public string? Hash { get; init; }
        public DateTime Timestamp { get; init; }
        public bool IsDirectory { get; init; }
    }

    public class SnapshotComparisonResult
    {
        public bool Success { get; init; }
        public string? ErrorMessage { get; init; }
        public string? Snapshot1Id { get; init; }
        public string? Snapshot2Id { get; init; }
        public DateTime Snapshot1Timestamp { get; init; }
        public DateTime Snapshot2Timestamp { get; init; }
        public List<FileDifference> AddedFiles { get; init; } = new();
        public List<FileDifference> RemovedFiles { get; init; } = new();
        public List<FileDifference> ModifiedFiles { get; init; } = new();
        public int TotalDifferences { get; init; }
    }

    public class FileDifference
    {
        public required string Path { get; init; }
        public ChangeType ChangeType { get; init; }
        public long OldSize { get; init; }
        public string? OldHash { get; init; }
        public DateTime OldTimestamp { get; init; }
        public long NewSize { get; init; }
        public string? NewHash { get; init; }
        public DateTime NewTimestamp { get; init; }
    }

    public enum ChangeType
    {
        Added,
        Removed,
        Modified
    }

    public class FileHistoryResult
    {
        public required string FilePath { get; init; }
        public int TotalVersions { get; init; }
        public List<FileVersion> Versions { get; init; } = new();
    }

    public class FileVersion
    {
        public required string SnapshotId { get; init; }
        public DateTime SnapshotTimestamp { get; init; }
        public required string FileHash { get; init; }
        public long FileSize { get; init; }
        public DateTime FileTimestamp { get; init; }
    }

    public class FileSearchResult
    {
        public required string SearchPattern { get; init; }
        public int TotalMatches { get; init; }
        public List<FileSearchMatch> Matches { get; init; } = new();
    }

    public class FileSearchMatch
    {
        public required string SnapshotId { get; init; }
        public DateTime SnapshotTimestamp { get; init; }
        public required string FilePath { get; init; }
        public long FileSize { get; init; }
        public required string FileHash { get; init; }
    }

    public class StorageStatistics
    {
        public int TotalSnapshots { get; init; }
        public int TotalFiles { get; init; }
        public long TotalBytes { get; init; }
        public DateTime? EarliestSnapshot { get; init; }
        public DateTime? LatestSnapshot { get; init; }
        public List<GranularityStats> ByGranularity { get; set; } = new();
        public List<GrowthDataPoint> GrowthTrend { get; set; } = new();
    }

    public class GranularityStats
    {
        public RestoreGranularity Granularity { get; init; }
        public int Count { get; init; }
        public long TotalBytes { get; init; }
    }

    public class GrowthDataPoint
    {
        public DateTime Timestamp { get; init; }
        public long CumulativeBytes { get; init; }
        public long SnapshotBytes { get; init; }
    }

    #endregion
}
