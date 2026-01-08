using DataWarehouse.SDK.AI.Math;

namespace DataWarehouse.SDK.AI.Events
{
    /// <summary>
    /// Base class for proactive agents.
    /// Agents observe system events and autonomously take action to optimize performance,
    /// reduce costs, improve security, or maintain data health.
    ///
    /// Proactive agents:
    /// - Subscribe to relevant events
    /// - Analyze patterns and trends
    /// - Detect anomalies
    /// - Automatically optimize
    /// - Alert admins when needed
    ///
    /// Agent types:
    /// - PerformanceOptimizationAgent: Detect slow operations, suggest improvements
    /// - CostOptimizationAgent: Find expensive operations, suggest alternatives
    /// - SecurityMonitoringAgent: Detect suspicious patterns, alert security team
    /// - DataHealthAgent: Monitor data quality, detect corruption
    /// </summary>
    public abstract class ProactiveAgent(EventBus eventBus, string agentName) : IEventHandler
    {
        protected readonly EventBus EventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        protected readonly string AgentName = agentName;
        protected bool IsRunning;

        /// <summary>
        /// Starts the agent.
        /// Subscribes to relevant events.
        /// </summary>
        public virtual void Start()
        {
            if (IsRunning)
                return;

            var eventTypes = GetMonitoredEventTypes();
            foreach (var eventType in eventTypes)
            {
                EventBus.Subscribe(eventType, this);
            }

            IsRunning = true;
            OnStart();
        }

        /// <summary>
        /// Stops the agent.
        /// Unsubscribes from events.
        /// </summary>
        public virtual void Stop()
        {
            if (!IsRunning)
                return;

            var eventTypes = GetMonitoredEventTypes();
            foreach (var eventType in eventTypes)
            {
                EventBus.Unsubscribe(eventType, this);
            }

            IsRunning = false;
            OnStop();
        }

        /// <summary>
        /// Handles an event.
        /// Implemented by IEventHandler interface.
        /// </summary>
        public async Task HandleAsync(SystemEvent @event)
        {
            if (!IsRunning)
                return;

            try
            {
                await OnEventAsync(@event);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{AgentName}] Error handling event: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets list of event types this agent monitors.
        /// </summary>
        protected abstract string[] GetMonitoredEventTypes();

        /// <summary>
        /// Called when an event occurs.
        /// Override to implement agent logic.
        /// </summary>
        protected abstract Task OnEventAsync(SystemEvent @event);

        /// <summary>
        /// Called when agent starts.
        /// Override for initialization.
        /// </summary>
        protected virtual void OnStart() { }

        /// <summary>
        /// Called when agent stops.
        /// Override for cleanup.
        /// </summary>
        protected virtual void OnStop() { }
    }

    /// <summary>
    /// Agent that monitors performance and suggests optimizations.
    /// </summary>
    public class PerformanceOptimizationAgent(EventBus eventBus) : ProactiveAgent(eventBus, "PerformanceOptimizer")
    {
        private readonly StatisticalAnalyzer _analyzer = new();
        private readonly List<double> _recentDurations = [];
        private const int MaxHistorySize = 50;

        protected override string[] GetMonitoredEventTypes()
        {
            return ["SlowOperation", "CapabilityExecuted"];
        }

        protected override async Task OnEventAsync(SystemEvent @event)
        {
            if (@event.EventType == "SlowOperation")
            {
                await HandleSlowOperationAsync(@event);
            }
            else if (@event.EventType == "CapabilityExecuted")
            {
                await TrackPerformanceAsync(@event);
            }
        }

        private async Task HandleSlowOperationAsync(SystemEvent @event)
        {
            var capabilityId = @event.Data.TryGetValue("capabilityId", out var cap) ? cap.ToString() : "unknown";
            var durationMs = @event.Data.TryGetValue("durationMs", out var dur) ? Convert.ToDouble(dur) : 0;

            Console.WriteLine($"[{AgentName}] Slow operation detected: {capabilityId} took {durationMs:F0}ms");

            // In real implementation:
            // - Look for alternative capabilities
            // - Suggest caching
            // - Recommend scaling
            // - Auto-optimize if safe

            await Task.CompletedTask;
        }

        private async Task TrackPerformanceAsync(SystemEvent @event)
        {
            if (@event.Data.TryGetValue("durationMs", out var dur))
            {
                var duration = Convert.ToDouble(dur);
                _recentDurations.Add(duration);

                if (_recentDurations.Count > MaxHistorySize)
                {
                    _recentDurations.RemoveAt(0);
                }

                // Analyze trend every 10 executions
                if (_recentDurations.Count >= 10 && _recentDurations.Count % 10 == 0)
                {
                    var trend = StatisticalAnalyzer.AnalyzeTrend(_recentDurations);
                    if (trend.HasTrend && trend.TrendDirection == TrendDirection.Increasing)
                    {
                        Console.WriteLine($"[{AgentName}] Performance degradation detected (slope: {trend.Slope:F2})");
                        // Alert admin or auto-optimize
                    }
                }
            }

            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// Agent that monitors costs and suggests cheaper alternatives.
    /// </summary>
    public class CostOptimizationAgent(EventBus eventBus) : ProactiveAgent(eventBus, "CostOptimizer")
    {
        private decimal _totalCost = 0;
        private const decimal DailyCostThreshold = 10.00m;

        protected override string[] GetMonitoredEventTypes()
        {
            return ["CapabilityExecuted", "LLMRequestCompleted"];
        }

        protected override async Task OnEventAsync(SystemEvent @event)
        {
            if (@event.Data.TryGetValue("costUsd", out var cost))
            {
                var costUsd = Convert.ToDecimal(cost);
                _totalCost += costUsd;

                if (_totalCost > DailyCostThreshold)
                {
                    Console.WriteLine($"[{AgentName}] Daily cost threshold exceeded: ${_totalCost:F2} > ${DailyCostThreshold:F2}");
                    // Alert admin or suggest optimizations
                }
            }

            await Task.CompletedTask;
        }

        protected override void OnStart()
        {
            // Reset daily cost tracking
            _totalCost = 0;
        }
    }

    /// <summary>
    /// Agent that monitors security events and detects threats.
    /// </summary>
    public class SecurityMonitoringAgent(EventBus eventBus) : ProactiveAgent(eventBus, "SecurityMonitor")
    {
        private readonly Dictionary<string, int> _accessAttempts = [];
        private const int SuspiciousAccessThreshold = 10;

        protected override string[] GetMonitoredEventTypes()
        {
            return ["BlobAccessed", "BlobDeleted", "UnauthorizedAccess", "SafetyViolation"];
        }

        protected override async Task OnEventAsync(SystemEvent @event)
        {
            if (@event.EventType == "BlobAccessed")
            {
                await TrackAccessPatternAsync(@event);
            }
            else if (@event.EventType == "SafetyViolation")
            {
                Console.WriteLine($"[{AgentName}] Safety violation detected: {(@event.Data.TryGetValue("message", out var msg) ? msg : "Unknown")}");
            }
            else if (@event.EventType == "UnauthorizedAccess")
            {
                Console.WriteLine($"[{AgentName}] ALERT: Unauthorized access attempt");
                // Trigger security response
            }
        }

        private async Task TrackAccessPatternAsync(SystemEvent @event)
        {
            if (@event.Data.TryGetValue("userId", out var user))
            {
                var userId = user.ToString() ?? "anonymous";

                if (!_accessAttempts.TryGetValue(userId, out int value))
                {
                    value = 0;
                    _accessAttempts[userId] = value;
                }

                _accessAttempts[userId] = ++value;

                if (value > SuspiciousAccessThreshold)
                {
                    Console.WriteLine($"[{AgentName}] Suspicious access pattern: User '{userId}' accessed {value} times");
                    // Alert security team
                }
            }

            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// Agent that monitors data health and detects corruption.
    /// </summary>
    public class DataHealthAgent(EventBus eventBus) : ProactiveAgent(eventBus, "DataHealth")
    {
        private readonly List<string> _recentErrors = [];
        private const int ErrorThreshold = 5;

        protected override string[] GetMonitoredEventTypes()
        {
            return ["CapabilityFailed", "DataCorruption", "ChecksumMismatch"];
        }

        protected override async Task OnEventAsync(SystemEvent @event)
        {
            _recentErrors.Add(@event.EventType);

            if (_recentErrors.Count > 100)
            {
                _recentErrors.RemoveAt(0);
            }

            // Check error rate
            var recentErrorCount = _recentErrors.Count;
            if (recentErrorCount > ErrorThreshold)
            {
                Console.WriteLine($"[{AgentName}] High error rate detected: {recentErrorCount} errors");

                // Analyze error patterns
                var errorsByType = new Dictionary<string, int>();
                foreach (var errorType in _recentErrors)
                {
                    if (!errorsByType.TryGetValue(errorType, out int value))
                    {
                        value = 0;
                        errorsByType[errorType] = value;
                    }
                    errorsByType[errorType] = ++value;
                }

                Console.WriteLine($"[{AgentName}] Error breakdown:");
                foreach (var kvp in errorsByType)
                {
                    Console.WriteLine($"  - {kvp.Key}: {kvp.Value}");
                }

                // Trigger health check or data validation
            }

            await Task.CompletedTask;
        }
    }
}
