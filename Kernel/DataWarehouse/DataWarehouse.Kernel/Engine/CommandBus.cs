using DataWarehouse.SDK.Contracts;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DataWarehouse.Kernel.Engine
{
    /// <summary>
    /// Production-Ready Command Bus for Message-Based Architecture.
    /// Enables decoupled communication between Kernel, Plugins, and AI Agents.
    /// All plugin operations are invoked through commands, not direct function calls.
    /// Thread-safe with automatic retry, circuit breaker, and telemetry.
    /// </summary>
    public class CommandBus : IDisposable
    {
        private readonly IKernelContext _context;
        private readonly ConcurrentDictionary<string, CommandHandler> _handlers;
        private readonly ConcurrentDictionary<string, CommandMetrics> _metrics;
        private readonly SemaphoreSlim _executionSemaphore;
        private readonly int _maxRetries = 3;
        private bool _disposed;

        /// <summary>
        /// Base command interface.
        /// </summary>
        public interface ICommand
        {
            string CommandId { get; }
            string CommandType { get; }
            Dictionary<string, object> Parameters { get; }
            string? InitiatedBy { get; } // User ID or "AI-Agent"
        }

        /// <summary>
        /// Base command result.
        /// </summary>
        public class CommandResult
        {
            public bool Success { get; set; }
            public object? Data { get; set; }
            public string? ErrorMessage { get; set; }
            public Exception? Exception { get; set; }
            public TimeSpan ExecutionTime { get; set; }
            public Dictionary<string, object> Metadata { get; set; } = new();
        }

        /// <summary>
        /// Command handler delegate.
        /// </summary>
        public delegate Task<CommandResult> CommandHandler(ICommand command, CancellationToken cancellationToken);

        /// <summary>
        /// Command execution metrics.
        /// </summary>
        private class CommandMetrics
        {
            public string CommandType { get; set; } = string.Empty;
            public int TotalExecutions { get; set; }
            public int SuccessCount { get; set; }
            public int FailureCount { get; set; }
            public TimeSpan TotalExecutionTime { get; set; }
            public DateTime LastExecuted { get; set; }
            public int CircuitBreakerFailures { get; set; }
            public DateTime? CircuitBreakerOpenedAt { get; set; }
        }

        /// <summary>
        /// Initialize command bus.
        /// </summary>
        public CommandBus(IKernelContext context, int maxConcurrentCommands = 100)
        {
            _context = context;
            _handlers = new ConcurrentDictionary<string, CommandHandler>();
            _metrics = new ConcurrentDictionary<string, CommandMetrics>();
            _executionSemaphore = new SemaphoreSlim(maxConcurrentCommands, maxConcurrentCommands);

            _context?.LogInfo("[CommandBus] Initialized with max concurrent commands: " + maxConcurrentCommands);
        }

        /// <summary>
        /// Register a command handler.
        /// </summary>
        public void RegisterHandler(string commandType, CommandHandler handler)
        {
            if (_handlers.TryAdd(commandType, handler))
            {
                _context?.LogInfo($"[CommandBus] Registered handler for command: {commandType}");

                // Initialize metrics
                _metrics[commandType] = new CommandMetrics
                {
                    CommandType = commandType
                };
            }
            else
            {
                _context?.LogWarning($"[CommandBus] Handler for '{commandType}' already registered, replacing");
                _handlers[commandType] = handler;
            }
        }

        /// <summary>
        /// Unregister a command handler.
        /// </summary>
        public bool UnregisterHandler(string commandType)
        {
            if (_handlers.TryRemove(commandType, out _))
            {
                _context?.LogInfo($"[CommandBus] Unregistered handler for command: {commandType}");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Execute a command with retry logic and circuit breaker.
        /// </summary>
        public async Task<CommandResult> ExecuteAsync(ICommand command, CancellationToken cancellationToken = default)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                await _executionSemaphore.WaitAsync(cancellationToken);

                try
                {
                    _context?.LogInfo($"[CommandBus] Executing command '{command.CommandType}' (ID: {command.CommandId})");

                    // Check circuit breaker
                    if (IsCircuitBreakerOpen(command.CommandType))
                    {
                        return new CommandResult
                        {
                            Success = false,
                            ErrorMessage = "Circuit breaker is open for this command type",
                            ExecutionTime = sw.Elapsed
                        };
                    }

                    // Get handler
                    if (!_handlers.TryGetValue(command.CommandType, out var handler))
                    {
                        return new CommandResult
                        {
                            Success = false,
                            ErrorMessage = $"No handler registered for command type: {command.CommandType}",
                            ExecutionTime = sw.Elapsed
                        };
                    }

                    // Execute with retry
                    CommandResult? result = null;
                    Exception? lastException = null;

                    for (int attempt = 0; attempt <= _maxRetries; attempt++)
                    {
                        try
                        {
                            if (attempt > 0)
                            {
                                var delay = TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 100);
                                _context?.LogWarning($"[CommandBus] Retry attempt {attempt} for '{command.CommandType}' after {delay.TotalMilliseconds}ms");
                                await Task.Delay(delay, cancellationToken);
                            }

                            result = await handler(command, cancellationToken);

                            if (result.Success)
                            {
                                sw.Stop();
                                result.ExecutionTime = sw.Elapsed;

                                UpdateMetrics(command.CommandType, true, sw.Elapsed);
                                _context?.LogInfo($"[CommandBus] Command '{command.CommandType}' completed successfully in {sw.ElapsedMilliseconds}ms");

                                return result;
                            }
                            else
                            {
                                lastException = result.Exception;
                            }
                        }
                        catch (Exception ex)
                        {
                            lastException = ex;
                            _context?.LogWarning($"[CommandBus] Command '{command.CommandType}' attempt {attempt + 1} failed: {ex.Message}");
                        }
                    }

                    // All retries failed
                    sw.Stop();
                    UpdateMetrics(command.CommandType, false, sw.Elapsed);
                    UpdateCircuitBreaker(command.CommandType);

                    return new CommandResult
                    {
                        Success = false,
                        ErrorMessage = $"Command failed after {_maxRetries} retries",
                        Exception = lastException,
                        ExecutionTime = sw.Elapsed
                    };
                }
                finally
                {
                    _executionSemaphore.Release();
                }
            }
            catch (OperationCanceledException)
            {
                _context?.LogWarning($"[CommandBus] Command '{command.CommandType}' was cancelled");

                return new CommandResult
                {
                    Success = false,
                    ErrorMessage = "Command execution was cancelled",
                    ExecutionTime = sw.Elapsed
                };
            }
        }

        /// <summary>
        /// Execute a command and return typed result.
        /// </summary>
        public async Task<T?> ExecuteAsync<T>(ICommand command, CancellationToken cancellationToken = default)
        {
            var result = await ExecuteAsync(command, cancellationToken);

            if (result.Success && result.Data is T typedData)
            {
                return typedData;
            }

            return default;
        }

        /// <summary>
        /// Execute multiple commands in parallel.
        /// </summary>
        public async Task<List<CommandResult>> ExecuteBatchAsync(IEnumerable<ICommand> commands, CancellationToken cancellationToken = default)
        {
            var tasks = commands.Select(cmd => ExecuteAsync(cmd, cancellationToken));
            var results = await Task.WhenAll(tasks);
            return results.ToList();
        }

        /// <summary>
        /// Check if handler exists for command type.
        /// </summary>
        public bool HasHandler(string commandType)
        {
            return _handlers.ContainsKey(commandType);
        }

        /// <summary>
        /// Get all registered command types.
        /// </summary>
        public List<string> GetRegisteredCommands()
        {
            return _handlers.Keys.ToList();
        }

        /// <summary>
        /// Get command execution metrics.
        /// </summary>
        public CommandMetrics? GetCommandMetrics(string commandType)
        {
            _metrics.TryGetValue(commandType, out var metrics);
            return metrics;
        }

        /// <summary>
        /// Get all command metrics.
        /// </summary>
        public Dictionary<string, object> GetAllMetrics()
        {
            var metricsData = new Dictionary<string, object>();

            foreach (var kvp in _metrics)
            {
                var metrics = kvp.Value;
                metricsData[kvp.Key] = new
                {
                    metrics.TotalExecutions,
                    metrics.SuccessCount,
                    metrics.FailureCount,
                    SuccessRate = metrics.TotalExecutions > 0
                        ? (double)metrics.SuccessCount / metrics.TotalExecutions * 100
                        : 0,
                    AverageExecutionTime = metrics.TotalExecutions > 0
                        ? metrics.TotalExecutionTime.TotalMilliseconds / metrics.TotalExecutions
                        : 0,
                    metrics.LastExecuted,
                    CircuitBreakerOpen = IsCircuitBreakerOpen(kvp.Key)
                };
            }

            return metricsData;
        }

        // ==================== CIRCUIT BREAKER ====================

        private const int CircuitBreakerThreshold = 5; // Failures before opening
        private readonly TimeSpan CircuitBreakerTimeout = TimeSpan.FromMinutes(1);

        private void UpdateCircuitBreaker(string commandType)
        {
            if (_metrics.TryGetValue(commandType, out var metrics))
            {
                metrics.CircuitBreakerFailures++;

                if (metrics.CircuitBreakerFailures >= CircuitBreakerThreshold)
                {
                    metrics.CircuitBreakerOpenedAt = DateTime.UtcNow;
                    _context?.LogError($"[CommandBus] Circuit breaker OPENED for command: {commandType}", null);
                }
            }
        }

        private bool IsCircuitBreakerOpen(string commandType)
        {
            if (!_metrics.TryGetValue(commandType, out var metrics))
                return false;

            if (metrics.CircuitBreakerOpenedAt == null)
                return false;

            // Check if timeout has elapsed
            if (DateTime.UtcNow - metrics.CircuitBreakerOpenedAt > CircuitBreakerTimeout)
            {
                // Reset circuit breaker
                metrics.CircuitBreakerOpenedAt = null;
                metrics.CircuitBreakerFailures = 0;
                _context?.LogInfo($"[CommandBus] Circuit breaker CLOSED for command: {commandType}");
                return false;
            }

            return true;
        }

        // ==================== METRICS ====================

        private void UpdateMetrics(string commandType, bool success, TimeSpan executionTime)
        {
            if (_metrics.TryGetValue(commandType, out var metrics))
            {
                metrics.TotalExecutions++;
                metrics.LastExecuted = DateTime.UtcNow;
                metrics.TotalExecutionTime += executionTime;

                if (success)
                {
                    metrics.SuccessCount++;
                    // Reset circuit breaker on success
                    metrics.CircuitBreakerFailures = 0;
                    metrics.CircuitBreakerOpenedAt = null;
                }
                else
                {
                    metrics.FailureCount++;
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            _context?.LogInfo("[CommandBus] Shutting down...");

            _handlers.Clear();
            _metrics.Clear();
            _executionSemaphore?.Dispose();

            _disposed = true;
        }
    }

    // ==================== STANDARD COMMANDS ====================

    /// <summary>
    /// Generic command implementation.
    /// </summary>
    public class Command : CommandBus.ICommand
    {
        public string CommandId { get; set; } = Guid.NewGuid().ToString();
        public string CommandType { get; set; } = string.Empty;
        public Dictionary<string, object> Parameters { get; set; } = new();
        public string? InitiatedBy { get; set; }
    }

    /// <summary>
    /// Standard storage command types.
    /// </summary>
    public static class StorageCommands
    {
        public const string SaveBlob = "Storage.SaveBlob";
        public const string LoadBlob = "Storage.LoadBlob";
        public const string DeleteBlob = "Storage.DeleteBlob";
        public const string ExistsBlob = "Storage.ExistsBlob";
        public const string ListBlobs = "Storage.ListBlobs";
    }

    /// <summary>
    /// Standard transformation command types.
    /// </summary>
    public static class TransformationCommands
    {
        public const string Compress = "Transform.Compress";
        public const string Decompress = "Transform.Decompress";
        public const string Encrypt = "Transform.Encrypt";
        public const string Decrypt = "Transform.Decrypt";
        public const string Deduplicate = "Transform.Deduplicate";
    }

    /// <summary>
    /// Standard governance command types.
    /// </summary>
    public static class GovernanceCommands
    {
        public const string EvaluatePolicy = "Governance.EvaluatePolicy";
        public const string ApplyCompliance = "Governance.ApplyCompliance";
        public const string CheckAccess = "Governance.CheckAccess";
        public const string AuditLog = "Governance.AuditLog";
    }

    /// <summary>
    /// Standard AI agent command types.
    /// </summary>
    public static class AgentCommands
    {
        public const string AnalyzePerformance = "Agent.AnalyzePerformance";
        public const string OptimizeCost = "Agent.OptimizeCost";
        public const string DetectAnomaly = "Agent.DetectAnomaly";
        public const string GenerateInsights = "Agent.GenerateInsights";
        public const string AutoHeal = "Agent.AutoHeal";
    }
}
