using DataWarehouse.SDK.Contracts;
using DataWarehouse.SDK.Security;
using System.Collections.Concurrent;
using System.Text.Json;

namespace DataWarehouse.Kernel.Security
{
    /// <summary>
    /// Comprehensive audit logging system for security, compliance, and forensics.
    /// Tracks all data access, modifications, authentication events, and system changes.
    /// </summary>
    public class AuditLogger
    {
        private readonly IKernelContext _context;
        private readonly ConcurrentQueue<AuditEvent> _eventQueue = new();
        private readonly string _logDirectory;
        private readonly object _fileLock = new();

        // Configuration
        private readonly int _maxQueueSize = 10000;
        private readonly TimeSpan _flushInterval = TimeSpan.FromSeconds(30);
        private readonly bool _enableDetailedLogging = true;
        private CancellationTokenSource? _flushCancellation;

        public AuditLogger(IKernelContext context, string? logDirectory = null)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logDirectory = logDirectory ?? Path.Combine(context.RootPath, "audit_logs");
            Directory.CreateDirectory(_logDirectory);
        }

        /// <summary>
        /// Start the audit logger background flush task.
        /// </summary>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _flushCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _context.LogInfo($"[Audit] Started audit logger, log directory: {_logDirectory}");

            // Start background flush task
            _ = Task.Run(async () => await FlushLoopAsync(_flushCancellation.Token), cancellationToken);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Stop the audit logger and flush remaining events.
        /// </summary>
        public async Task StopAsync()
        {
            _flushCancellation?.Cancel();
            await FlushAsync(); // Final flush
            _context.LogInfo("[Audit] Stopped audit logger");
        }

        /// <summary>
        /// Log data access event.
        /// </summary>
        public void LogDataAccess(
            string userId,
            string username,
            string key,
            AuditAction action,
            bool success,
            string? errorMessage = null,
            Dictionary<string, object>? metadata = null)
        {
            var auditEvent = new AuditEvent
            {
                EventId = Guid.NewGuid().ToString(),
                Timestamp = DateTime.UtcNow,
                Category = AuditCategory.DataAccess,
                Action = action,
                UserId = userId,
                Username = username,
                ResourceType = "Data",
                ResourceId = key,
                Success = success,
                ErrorMessage = errorMessage,
                Metadata = metadata ?? new Dictionary<string, object>()
            };

            EnqueueEvent(auditEvent);
        }

        /// <summary>
        /// Log authentication event.
        /// </summary>
        public void LogAuthentication(
            string username,
            AuthenticationMethod method,
            bool success,
            string? ipAddress = null,
            string? errorMessage = null)
        {
            var auditEvent = new AuditEvent
            {
                EventId = Guid.NewGuid().ToString(),
                Timestamp = DateTime.UtcNow,
                Category = AuditCategory.Authentication,
                Action = success ? AuditAction.Login : AuditAction.LoginFailed,
                Username = username,
                Success = success,
                ErrorMessage = errorMessage,
                IpAddress = ipAddress,
                Metadata = new Dictionary<string, object>
                {
                    ["method"] = method.ToString()
                }
            };

            EnqueueEvent(auditEvent);
        }

        /// <summary>
        /// Log authorization failure.
        /// </summary>
        public void LogAuthorizationFailure(
            string userId,
            string username,
            Permission requiredPermission,
            string? resource = null)
        {
            var auditEvent = new AuditEvent
            {
                EventId = Guid.NewGuid().ToString(),
                Timestamp = DateTime.UtcNow,
                Category = AuditCategory.Authorization,
                Action = AuditAction.PermissionDenied,
                UserId = userId,
                Username = username,
                ResourceType = "Permission",
                ResourceId = resource,
                Success = false,
                Metadata = new Dictionary<string, object>
                {
                    ["required_permission"] = requiredPermission.ToString()
                }
            };

            EnqueueEvent(auditEvent);
        }

        /// <summary>
        /// Log configuration change.
        /// </summary>
        public void LogConfigurationChange(
            string userId,
            string username,
            string configKey,
            object? oldValue,
            object? newValue)
        {
            var auditEvent = new AuditEvent
            {
                EventId = Guid.NewGuid().ToString(),
                Timestamp = DateTime.UtcNow,
                Category = AuditCategory.Configuration,
                Action = AuditAction.Update,
                UserId = userId,
                Username = username,
                ResourceType = "Configuration",
                ResourceId = configKey,
                Success = true,
                Metadata = new Dictionary<string, object>
                {
                    ["old_value"] = oldValue ?? "null",
                    ["new_value"] = newValue ?? "null"
                }
            };

            EnqueueEvent(auditEvent);
        }

        /// <summary>
        /// Log system event.
        /// </summary>
        public void LogSystemEvent(
            AuditAction action,
            string description,
            bool success = true,
            Dictionary<string, object>? metadata = null)
        {
            var auditEvent = new AuditEvent
            {
                EventId = Guid.NewGuid().ToString(),
                Timestamp = DateTime.UtcNow,
                Category = AuditCategory.System,
                Action = action,
                Username = "SYSTEM",
                ResourceType = "System",
                Success = success,
                Description = description,
                Metadata = metadata ?? new Dictionary<string, object>()
            };

            EnqueueEvent(auditEvent);
        }

        /// <summary>
        /// Log security incident.
        /// </summary>
        public void LogSecurityIncident(
            string username,
            string incidentType,
            string description,
            SecuritySeverity severity,
            string? ipAddress = null,
            Dictionary<string, object>? metadata = null)
        {
            var auditEvent = new AuditEvent
            {
                EventId = Guid.NewGuid().ToString(),
                Timestamp = DateTime.UtcNow,
                Category = AuditCategory.Security,
                Action = AuditAction.SecurityIncident,
                Username = username,
                ResourceType = incidentType,
                Description = description,
                Success = false,
                IpAddress = ipAddress,
                Severity = severity,
                Metadata = metadata ?? new Dictionary<string, object>()
            };

            EnqueueEvent(auditEvent);

            // Also log to kernel for immediate visibility
            _context.LogWarning($"[SECURITY] {severity}: {description} (User: {username})");
        }

        /// <summary>
        /// Query audit logs with filters.
        /// </summary>
        public async Task<List<AuditEvent>> QueryLogsAsync(AuditQuery query)
        {
            var results = new List<AuditEvent>();
            var startDate = query.StartTime?.Date ?? DateTime.UtcNow.AddDays(-7).Date;
            var endDate = query.EndTime?.Date ?? DateTime.UtcNow.Date;

            // Iterate through log files in date range
            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                var logFile = GetLogFilePath(date);
                if (!File.Exists(logFile))
                    continue;

                var fileEvents = await ReadLogFileAsync(logFile);
                var filtered = FilterEvents(fileEvents, query);
                results.AddRange(filtered);

                if (query.Limit > 0 && results.Count >= query.Limit)
                {
                    results = results.Take(query.Limit).ToList();
                    break;
                }
            }

            return results.OrderByDescending(e => e.Timestamp).ToList();
        }

        /// <summary>
        /// Get audit statistics for a time period.
        /// </summary>
        public async Task<AuditStatistics> GetStatisticsAsync(DateTime startTime, DateTime endTime)
        {
            var events = await QueryLogsAsync(new AuditQuery
            {
                StartTime = startTime,
                EndTime = endTime
            });

            var stats = new AuditStatistics
            {
                StartTime = startTime,
                EndTime = endTime,
                TotalEvents = events.Count,
                EventsByCategory = events.GroupBy(e => e.Category)
                    .ToDictionary(g => g.Key, g => g.Count()),
                EventsByAction = events.GroupBy(e => e.Action)
                    .ToDictionary(g => g.Key, g => g.Count()),
                FailedEvents = events.Count(e => !e.Success),
                UniqueUsers = events.Where(e => e.UserId != null)
                    .Select(e => e.UserId!)
                    .Distinct()
                    .Count(),
                SecurityIncidents = events.Count(e => e.Category == AuditCategory.Security)
            };

            return stats;
        }

        private void EnqueueEvent(AuditEvent auditEvent)
        {
            _eventQueue.Enqueue(auditEvent);

            // Check queue size and flush if needed
            if (_eventQueue.Count >= _maxQueueSize)
            {
                _ = Task.Run(() => FlushAsync());
            }

            if (_enableDetailedLogging)
            {
                _context.LogDebug($"[Audit] {auditEvent.Category}/{auditEvent.Action}: {auditEvent.Username} - {auditEvent.ResourceId}");
            }
        }

        private async Task FlushLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_flushInterval, cancellationToken);
                    await FlushAsync();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _context.LogError("[Audit] Flush loop error", ex);
                }
            }
        }

        private async Task FlushAsync()
        {
            if (_eventQueue.IsEmpty)
                return;

            var events = new List<AuditEvent>();
            while (_eventQueue.TryDequeue(out var evt) && events.Count < 1000)
            {
                events.Add(evt);
            }

            if (events.Count == 0)
                return;

            // Group events by date for file organization
            var eventsByDate = events.GroupBy(e => e.Timestamp.Date);

            foreach (var group in eventsByDate)
            {
                var logFile = GetLogFilePath(group.Key);
                await WriteEventsToFileAsync(logFile, group.ToList());
            }

            _context.LogDebug($"[Audit] Flushed {events.Count} audit events");
        }

        private string GetLogFilePath(DateTime date)
        {
            var fileName = $"audit_{date:yyyy-MM-dd}.jsonl";
            return Path.Combine(_logDirectory, fileName);
        }

        private async Task WriteEventsToFileAsync(string filePath, List<AuditEvent> events)
        {
            lock (_fileLock)
            {
                using var writer = new StreamWriter(filePath, append: true);
                foreach (var evt in events)
                {
                    var json = JsonSerializer.Serialize(evt, new JsonSerializerOptions
                    {
                        WriteIndented = false
                    });
                    writer.WriteLine(json);
                }
            }

            await Task.CompletedTask;
        }

        private async Task<List<AuditEvent>> ReadLogFileAsync(string filePath)
        {
            var events = new List<AuditEvent>();

            using var reader = new StreamReader(filePath);
            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                try
                {
                    var evt = JsonSerializer.Deserialize<AuditEvent>(line);
                    if (evt != null)
                        events.Add(evt);
                }
                catch (Exception ex)
                {
                    _context.LogWarning($"[Audit] Failed to parse audit event: {ex.Message}");
                }
            }

            return events;
        }

        private static List<AuditEvent> FilterEvents(List<AuditEvent> events, AuditQuery query)
        {
            var filtered = events.AsEnumerable();

            if (query.Category.HasValue)
                filtered = filtered.Where(e => e.Category == query.Category.Value);

            if (query.Action.HasValue)
                filtered = filtered.Where(e => e.Action == query.Action.Value);

            if (!string.IsNullOrEmpty(query.UserId))
                filtered = filtered.Where(e => e.UserId == query.UserId);

            if (!string.IsNullOrEmpty(query.Username))
                filtered = filtered.Where(e => e.Username?.Contains(query.Username, StringComparison.OrdinalIgnoreCase) == true);

            if (!string.IsNullOrEmpty(query.ResourceType))
                filtered = filtered.Where(e => e.ResourceType == query.ResourceType);

            if (!string.IsNullOrEmpty(query.ResourceId))
                filtered = filtered.Where(e => e.ResourceId?.Contains(query.ResourceId, StringComparison.OrdinalIgnoreCase) == true);

            if (query.SuccessOnly)
                filtered = filtered.Where(e => e.Success);

            return filtered.ToList();
        }
    }

    public class AuditEvent
    {
        public required string EventId { get; init; }
        public DateTime Timestamp { get; init; }
        public AuditCategory Category { get; init; }
        public AuditAction Action { get; init; }
        public string? UserId { get; init; }
        public string? Username { get; init; }
        public string? ResourceType { get; init; }
        public string? ResourceId { get; init; }
        public bool Success { get; init; }
        public string? ErrorMessage { get; init; }
        public string? Description { get; init; }
        public string? IpAddress { get; init; }
        public SecuritySeverity Severity { get; init; } = SecuritySeverity.Info;
        public Dictionary<string, object> Metadata { get; init; } = new();
    }

    public enum AuditCategory
    {
        Authentication,
        Authorization,
        DataAccess,
        Configuration,
        System,
        Security,
        Plugin
    }

    public enum AuditAction
    {
        // Authentication
        Login,
        Logout,
        LoginFailed,

        // Data operations
        Read,
        Write,
        Delete,
        Update,

        // Security
        PermissionDenied,
        SecurityIncident,

        // System
        Start,
        Stop,
        ConfigChange,
        PluginLoad,
        PluginUnload,

        // Other
        Unknown
    }

    public enum SecuritySeverity
    {
        Info,
        Low,
        Medium,
        High,
        Critical
    }

    public class AuditQuery
    {
        public DateTime? StartTime { get; init; }
        public DateTime? EndTime { get; init; }
        public AuditCategory? Category { get; init; }
        public AuditAction? Action { get; init; }
        public string? UserId { get; init; }
        public string? Username { get; init; }
        public string? ResourceType { get; init; }
        public string? ResourceId { get; init; }
        public bool SuccessOnly { get; init; }
        public int Limit { get; init; }
    }

    public class AuditStatistics
    {
        public DateTime StartTime { get; init; }
        public DateTime EndTime { get; init; }
        public int TotalEvents { get; init; }
        public Dictionary<AuditCategory, int> EventsByCategory { get; init; } = new();
        public Dictionary<AuditAction, int> EventsByAction { get; init; } = new();
        public int FailedEvents { get; init; }
        public int UniqueUsers { get; init; }
        public int SecurityIncidents { get; init; }
    }
}
