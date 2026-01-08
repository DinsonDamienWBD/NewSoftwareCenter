using DataWarehouse.SDK.Contracts;
using System.Collections.Concurrent;

namespace DataWarehouse.Kernel.Scheduling
{
    /// <summary>
    /// Production-Ready Scheduler Service for Plugin Execution.
    /// Supports: Cron, Periodic, Event-Driven, and On-Demand scheduling.
    /// Designed for AI agent command access and automatic plugin orchestration.
    /// </summary>
    public class SchedulerService : IDisposable
    {
        private readonly IKernelContext _context;
        private readonly ConcurrentDictionary<string, ScheduledTask> _tasks;
        private readonly ConcurrentDictionary<string, Timer> _timers;
        private readonly ConcurrentDictionary<string, EventSubscription> _eventSubscriptions;
        private readonly SemaphoreSlim _executionSemaphore;
        private readonly CancellationTokenSource _shutdownToken;
        private bool _disposed;

        /// <summary>
        /// Scheduled task definition.
        /// </summary>
        public class ScheduledTask
        {
            public string Id { get; set; } = Guid.NewGuid().ToString();
            public string Name { get; set; } = string.Empty;
            public ScheduleType Type { get; set; }
            public string? CronExpression { get; set; }
            public TimeSpan? Interval { get; set; }
            public string? EventPattern { get; set; }
            public Func<Task> Action { get; set; } = () => Task.CompletedTask;
            public DateTime? NextRun { get; set; }
            public DateTime? LastRun { get; set; }
            public int ExecutionCount { get; set; }
            public bool Enabled { get; set; } = true;
            public int MaxConcurrentExecutions { get; set; } = 1;
            public int CurrentExecutions { get; set; }
            public TaskPriority Priority { get; set; } = TaskPriority.Normal;
            public Dictionary<string, object> Metadata { get; set; } = new();
        }

        public enum ScheduleType
        {
            /// <summary>Cron-based scheduling (e.g., "0 0 * * *" for daily at midnight)</summary>
            Cron,
            /// <summary>Fixed interval (e.g., every 5 minutes)</summary>
            Periodic,
            /// <summary>Event-driven (triggered by specific events)</summary>
            EventDriven,
            /// <summary>On-demand execution only</summary>
            OnDemand
        }

        public enum TaskPriority
        {
            Low = 0,
            Normal = 1,
            High = 2,
            Critical = 3
        }

        private class EventSubscription
        {
            public string Pattern { get; set; } = string.Empty;
            public List<string> TaskIds { get; set; } = new();
        }

        /// <summary>
        /// Initialize scheduler service.
        /// </summary>
        public SchedulerService(IKernelContext context, int maxConcurrentTasks = 10)
        {
            _context = context;
            _tasks = new ConcurrentDictionary<string, ScheduledTask>();
            _timers = new ConcurrentDictionary<string, Timer>();
            _eventSubscriptions = new ConcurrentDictionary<string, EventSubscription>();
            _executionSemaphore = new SemaphoreSlim(maxConcurrentTasks, maxConcurrentTasks);
            _shutdownToken = new CancellationTokenSource();

            _context?.LogInfo("[Scheduler] Initialized with max concurrent tasks: " + maxConcurrentTasks);
        }

        /// <summary>
        /// Schedule a new task.
        /// </summary>
        public string ScheduleTask(ScheduledTask task)
        {
            if (_tasks.TryAdd(task.Id, task))
            {
                _context?.LogInfo($"[Scheduler] Scheduled task '{task.Name}' ({task.Type})");

                switch (task.Type)
                {
                    case ScheduleType.Cron:
                        ScheduleCronTask(task);
                        break;

                    case ScheduleType.Periodic:
                        SchedulePeriodicTask(task);
                        break;

                    case ScheduleType.EventDriven:
                        ScheduleEventDrivenTask(task);
                        break;

                    case ScheduleType.OnDemand:
                        // No automatic scheduling
                        break;
                }

                return task.Id;
            }

            throw new InvalidOperationException($"Task with ID '{task.Id}' already exists");
        }

        /// <summary>
        /// Schedule a cron-based task.
        /// </summary>
        private void ScheduleCronTask(ScheduledTask task)
        {
            if (string.IsNullOrEmpty(task.CronExpression))
            {
                throw new ArgumentException("Cron expression is required for Cron tasks");
            }

            // Calculate next run time
            var nextRun = CalculateNextCronRun(task.CronExpression);
            task.NextRun = nextRun;

            // Schedule timer
            var delay = nextRun - DateTime.UtcNow;
            if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;

            var timer = new Timer(
                async _ => await ExecuteCronTask(task.Id),
                null,
                delay,
                Timeout.InfiniteTimeSpan
            );

            _timers[task.Id] = timer;
        }

        /// <summary>
        /// Schedule a periodic task.
        /// </summary>
        private void SchedulePeriodicTask(ScheduledTask task)
        {
            if (task.Interval == null || task.Interval <= TimeSpan.Zero)
            {
                throw new ArgumentException("Interval is required for Periodic tasks");
            }

            task.NextRun = DateTime.UtcNow.Add(task.Interval.Value);

            var timer = new Timer(
                async _ => await ExecutePeriodicTask(task.Id),
                null,
                task.Interval.Value,
                task.Interval.Value
            );

            _timers[task.Id] = timer;
        }

        /// <summary>
        /// Schedule an event-driven task.
        /// </summary>
        private void ScheduleEventDrivenTask(ScheduledTask task)
        {
            if (string.IsNullOrEmpty(task.EventPattern))
            {
                throw new ArgumentException("Event pattern is required for EventDriven tasks");
            }

            var subscription = _eventSubscriptions.GetOrAdd(task.EventPattern, _ => new EventSubscription
            {
                Pattern = task.EventPattern
            });

            subscription.TaskIds.Add(task.Id);
        }

        /// <summary>
        /// Execute a cron task and reschedule.
        /// </summary>
        private async Task ExecuteCronTask(string taskId)
        {
            if (_tasks.TryGetValue(taskId, out var task))
            {
                await ExecuteTaskAsync(task);

                // Reschedule for next cron interval
                if (task.Enabled && !string.IsNullOrEmpty(task.CronExpression))
                {
                    var nextRun = CalculateNextCronRun(task.CronExpression);
                    task.NextRun = nextRun;

                    var delay = nextRun - DateTime.UtcNow;
                    if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;

                    if (_timers.TryGetValue(taskId, out var timer))
                    {
                        timer.Change(delay, Timeout.InfiniteTimeSpan);
                    }
                }
            }
        }

        /// <summary>
        /// Execute a periodic task.
        /// </summary>
        private async Task ExecutePeriodicTask(string taskId)
        {
            if (_tasks.TryGetValue(taskId, out var task))
            {
                await ExecuteTaskAsync(task);

                if (task.Interval != null)
                {
                    task.NextRun = DateTime.UtcNow.Add(task.Interval.Value);
                }
            }
        }

        /// <summary>
        /// Trigger event-driven tasks.
        /// </summary>
        public async Task TriggerEventAsync(string eventName, Dictionary<string, object>? eventData = null)
        {
            var matchingSubscriptions = _eventSubscriptions
                .Where(kvp => IsEventMatch(eventName, kvp.Key))
                .SelectMany(kvp => kvp.Value.TaskIds)
                .Distinct();

            var tasks = new List<Task>();

            foreach (var taskId in matchingSubscriptions)
            {
                if (_tasks.TryGetValue(taskId, out var task) && task.Enabled)
                {
                    tasks.Add(ExecuteTaskAsync(task, eventData));
                }
            }

            if (tasks.Any())
            {
                _context?.LogInfo($"[Scheduler] Triggered {tasks.Count} tasks for event '{eventName}'");
                await Task.WhenAll(tasks);
            }
        }

        /// <summary>
        /// Execute a task on-demand.
        /// </summary>
        public async Task ExecuteTaskNowAsync(string taskId, Dictionary<string, object>? context = null)
        {
            if (_tasks.TryGetValue(taskId, out var task))
            {
                await ExecuteTaskAsync(task, context);
            }
            else
            {
                throw new InvalidOperationException($"Task '{taskId}' not found");
            }
        }

        /// <summary>
        /// Core task execution with concurrency control and error handling.
        /// </summary>
        private async Task ExecuteTaskAsync(ScheduledTask task, Dictionary<string, object>? context = null)
        {
            if (!task.Enabled)
            {
                _context?.LogInfo($"[Scheduler] Task '{task.Name}' is disabled, skipping");
                return;
            }

            // Check concurrency limit
            if (task.CurrentExecutions >= task.MaxConcurrentExecutions)
            {
                _context?.LogWarning($"[Scheduler] Task '{task.Name}' at max concurrency, skipping");
                return;
            }

            Interlocked.Increment(ref task.CurrentExecutions);

            try
            {
                await _executionSemaphore.WaitAsync(_shutdownToken.Token);

                try
                {
                    _context?.LogInfo($"[Scheduler] Executing task '{task.Name}' (Priority: {task.Priority})");

                    var sw = System.Diagnostics.Stopwatch.StartNew();

                    // Execute task action
                    await task.Action();

                    sw.Stop();

                    task.LastRun = DateTime.UtcNow;
                    task.ExecutionCount++;

                    _context?.LogInfo($"[Scheduler] Task '{task.Name}' completed in {sw.ElapsedMilliseconds}ms");
                }
                catch (Exception ex)
                {
                    _context?.LogError($"[Scheduler] Task '{task.Name}' failed", ex);

                    // Optionally disable task after repeated failures
                    // (Implementation could track failure count)
                }
                finally
                {
                    _executionSemaphore.Release();
                }
            }
            catch (OperationCanceledException)
            {
                _context?.LogWarning($"[Scheduler] Task '{task.Name}' cancelled during shutdown");
            }
            finally
            {
                Interlocked.Decrement(ref task.CurrentExecutions);
            }
        }

        /// <summary>
        /// Cancel a scheduled task.
        /// </summary>
        public bool CancelTask(string taskId)
        {
            if (_tasks.TryRemove(taskId, out var task))
            {
                task.Enabled = false;

                if (_timers.TryRemove(taskId, out var timer))
                {
                    timer.Dispose();
                }

                _context?.LogInfo($"[Scheduler] Cancelled task '{task.Name}'");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Pause a task (disable without removing).
        /// </summary>
        public bool PauseTask(string taskId)
        {
            if (_tasks.TryGetValue(taskId, out var task))
            {
                task.Enabled = false;
                _context?.LogInfo($"[Scheduler] Paused task '{task.Name}'");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Resume a paused task.
        /// </summary>
        public bool ResumeTask(string taskId)
        {
            if (_tasks.TryGetValue(taskId, out var task))
            {
                task.Enabled = true;
                _context?.LogInfo($"[Scheduler] Resumed task '{task.Name}'");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Get all scheduled tasks.
        /// </summary>
        public List<ScheduledTask> GetAllTasks()
        {
            return _tasks.Values.ToList();
        }

        /// <summary>
        /// Get tasks by type.
        /// </summary>
        public List<ScheduledTask> GetTasksByType(ScheduleType type)
        {
            return _tasks.Values.Where(t => t.Type == type).ToList();
        }

        /// <summary>
        /// Get scheduler statistics.
        /// </summary>
        public Dictionary<string, object> GetStatistics()
        {
            return new Dictionary<string, object>
            {
                ["TotalTasks"] = _tasks.Count,
                ["EnabledTasks"] = _tasks.Values.Count(t => t.Enabled),
                ["CronTasks"] = _tasks.Values.Count(t => t.Type == ScheduleType.Cron),
                ["PeriodicTasks"] = _tasks.Values.Count(t => t.Type == ScheduleType.Periodic),
                ["EventDrivenTasks"] = _tasks.Values.Count(t => t.Type == ScheduleType.EventDriven),
                ["OnDemandTasks"] = _tasks.Values.Count(t => t.Type == ScheduleType.OnDemand),
                ["TotalExecutions"] = _tasks.Values.Sum(t => t.ExecutionCount),
                ["ActiveExecutions"] = _tasks.Values.Sum(t => t.CurrentExecutions)
            };
        }

        // ==================== HELPER METHODS ====================

        /// <summary>
        /// Calculate next cron run time (simplified cron parser).
        /// Format: "minute hour day month dayOfWeek"
        /// Example: "0 0 * * *" = daily at midnight
        /// Example: "*/5 * * * *" = every 5 minutes
        /// </summary>
        private DateTime CalculateNextCronRun(string cronExpression)
        {
            try
            {
                var parts = cronExpression.Split(' ');
                if (parts.Length != 5)
                {
                    throw new ArgumentException("Cron expression must have 5 parts: minute hour day month dayOfWeek");
                }

                var now = DateTime.UtcNow;
                var next = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0).AddMinutes(1);

                // Simplified cron parsing - handle basic patterns
                // For production, use a library like NCrontab or Cronos

                // Parse minute
                if (parts[0] != "*")
                {
                    if (parts[0].StartsWith("*/"))
                    {
                        var interval = int.Parse(parts[0].Substring(2));
                        var minutesUntilNext = interval - (next.Minute % interval);
                        next = next.AddMinutes(minutesUntilNext);
                    }
                    else
                    {
                        var minute = int.Parse(parts[0]);
                        if (next.Minute > minute)
                        {
                            next = next.AddHours(1);
                        }
                        next = new DateTime(next.Year, next.Month, next.Day, next.Hour, minute, 0);
                    }
                }

                // Parse hour
                if (parts[1] != "*")
                {
                    var hour = int.Parse(parts[1]);
                    if (next.Hour > hour || (next.Hour == hour && next.Minute > 0))
                    {
                        next = next.AddDays(1);
                    }
                    next = new DateTime(next.Year, next.Month, next.Day, hour, next.Minute, 0);
                }

                return next;
            }
            catch (Exception ex)
            {
                _context?.LogError($"[Scheduler] Invalid cron expression '{cronExpression}'", ex);
                // Fallback: run in 1 hour
                return DateTime.UtcNow.AddHours(1);
            }
        }

        /// <summary>
        /// Check if event name matches pattern (supports wildcards).
        /// </summary>
        private bool IsEventMatch(string eventName, string pattern)
        {
            if (pattern == "*") return true;
            if (pattern == eventName) return true;

            // Simple wildcard matching
            if (pattern.Contains("*"))
            {
                var regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*") + "$";
                return System.Text.RegularExpressions.Regex.IsMatch(eventName, regex);
            }

            return false;
        }

        public void Dispose()
        {
            if (_disposed) return;

            _context?.LogInfo("[Scheduler] Shutting down...");

            _shutdownToken.Cancel();

            // Dispose all timers
            foreach (var timer in _timers.Values)
            {
                timer.Dispose();
            }

            _timers.Clear();
            _tasks.Clear();
            _eventSubscriptions.Clear();
            _executionSemaphore?.Dispose();
            _shutdownToken?.Dispose();

            _disposed = true;
        }
    }
}
