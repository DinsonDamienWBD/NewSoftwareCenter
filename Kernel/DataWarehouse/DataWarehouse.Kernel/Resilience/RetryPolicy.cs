using DataWarehouse.SDK.Contracts;

namespace DataWarehouse.DataWarehouse.Kernel.Resilience
{
    /// <summary>
    /// Implements retry logic with exponential backoff for transient failures.
    /// Provides configurable retry policies with jitter to prevent thundering herd.
    /// </summary>
    public class RetryPolicy
    {
        private readonly IKernelContext? _context;
        private readonly int _maxRetries;
        private readonly TimeSpan _initialDelay;
        private readonly TimeSpan _maxDelay;
        private readonly double _backoffMultiplier;
        private readonly bool _useJitter;
        private readonly Random _random = new();

        public RetryPolicy(
            int maxRetries = 3,
            TimeSpan? initialDelay = null,
            TimeSpan? maxDelay = null,
            double backoffMultiplier = 2.0,
            bool useJitter = true,
            IKernelContext? context = null)
        {
            _maxRetries = maxRetries;
            _initialDelay = initialDelay ?? TimeSpan.FromSeconds(1);
            _maxDelay = maxDelay ?? TimeSpan.FromMinutes(1);
            _backoffMultiplier = backoffMultiplier;
            _useJitter = useJitter;
            _context = context;
        }

        /// <summary>
        /// Execute operation with retry logic.
        /// </summary>
        public async Task<T> ExecuteAsync<T>(
            Func<Task<T>> operation,
            Func<Exception, bool>? shouldRetry = null,
            CancellationToken cancellationToken = default)
        {
            shouldRetry ??= IsTransientError;
            int attempt = 0;
            Exception? lastException = null;

            while (attempt <= _maxRetries)
            {
                try
                {
                    if (attempt > 0)
                    {
                        _context?.LogDebug($"[Retry] Attempt {attempt}/{_maxRetries}");
                    }

                    return await operation();
                }
                catch (Exception ex) when (attempt < _maxRetries && shouldRetry(ex))
                {
                    lastException = ex;
                    attempt++;

                    var delay = CalculateDelay(attempt);
                    _context?.LogWarning($"[Retry] Attempt {attempt} failed: {ex.Message}. Retrying in {delay.TotalSeconds:F1}s...");

                    await Task.Delay(delay, cancellationToken);
                }
            }

            _context?.LogError($"[Retry] All {_maxRetries} retry attempts exhausted", lastException);
            throw new RetryException($"Operation failed after {_maxRetries} retries", lastException!);
        }

        /// <summary>
        /// Execute operation with retry logic (void return).
        /// </summary>
        public async Task ExecuteAsync(
            Func<Task> operation,
            Func<Exception, bool>? shouldRetry = null,
            CancellationToken cancellationToken = default)
        {
            await ExecuteAsync(async () =>
            {
                await operation();
                return true;
            }, shouldRetry, cancellationToken);
        }

        /// <summary>
        /// Calculate delay for retry attempt with exponential backoff and jitter.
        /// </summary>
        private TimeSpan CalculateDelay(int attempt)
        {
            // Calculate base delay with exponential backoff
            var baseDelay = _initialDelay.TotalMilliseconds * Math.Pow(_backoffMultiplier, attempt - 1);

            // Cap at max delay
            baseDelay = Math.Min(baseDelay, _maxDelay.TotalMilliseconds);

            // Add jitter if enabled (±25% randomization)
            if (_useJitter)
            {
                var jitterFactor = 0.75 + (_random.NextDouble() * 0.5); // 0.75 to 1.25
                baseDelay *= jitterFactor;
            }

            return TimeSpan.FromMilliseconds(baseDelay);
        }

        /// <summary>
        /// Determine if error is transient and should be retried.
        /// </summary>
        private static bool IsTransientError(Exception ex)
        {
            // Network errors
            if (ex is HttpRequestException or TimeoutException)
                return true;

            // I/O errors
            if (ex is IOException ioEx)
            {
                // Retry on specific I/O errors (not all)
                var hResult = ioEx.HResult;
                return hResult switch
                {
                    unchecked((int)0x80070020) => true, // ERROR_SHARING_VIOLATION
                    unchecked((int)0x80070021) => true, // ERROR_LOCK_VIOLATION
                    _ => false
                };
            }

            // SQL transient errors (if using database)
            if (ex.GetType().Name.Contains("SqlException"))
            {
                // Common transient error codes: deadlock, timeout, connection errors
                return true;
            }

            // Task cancellation is not transient
            if (ex is OperationCanceledException)
                return false;

            // Default: don't retry
            return false;
        }

        /// <summary>
        /// Create a retry policy optimized for network operations.
        /// </summary>
        public static RetryPolicy ForNetwork(IKernelContext? context = null) =>
            new RetryPolicy(
                maxRetries: 4,
                initialDelay: TimeSpan.FromSeconds(2),
                maxDelay: TimeSpan.FromSeconds(16),
                backoffMultiplier: 2.0,
                useJitter: true,
                context: context);

        /// <summary>
        /// Create a retry policy optimized for database operations.
        /// </summary>
        public static RetryPolicy ForDatabase(IKernelContext? context = null) =>
            new RetryPolicy(
                maxRetries: 3,
                initialDelay: TimeSpan.FromMilliseconds(500),
                maxDelay: TimeSpan.FromSeconds(5),
                backoffMultiplier: 2.0,
                useJitter: true,
                context: context);

        /// <summary>
        /// Create a retry policy optimized for storage operations.
        /// </summary>
        public static RetryPolicy ForStorage(IKernelContext? context = null) =>
            new RetryPolicy(
                maxRetries: 5,
                initialDelay: TimeSpan.FromMilliseconds(100),
                maxDelay: TimeSpan.FromSeconds(10),
                backoffMultiplier: 2.0,
                useJitter: true,
                context: context);

        /// <summary>
        /// Create a fast retry policy for quick operations.
        /// </summary>
        public static RetryPolicy Fast(IKernelContext? context = null) =>
            new RetryPolicy(
                maxRetries: 2,
                initialDelay: TimeSpan.FromMilliseconds(100),
                maxDelay: TimeSpan.FromSeconds(1),
                backoffMultiplier: 2.0,
                useJitter: false,
                context: context);
    }

    /// <summary>
    /// Circuit breaker pattern to prevent cascading failures.
    /// </summary>
    public class CircuitBreaker
    {
        private readonly int _failureThreshold;
        private readonly TimeSpan _resetTimeout;
        private readonly IKernelContext? _context;

        private int _failureCount = 0;
        private DateTime? _lastFailureTime;
        private CircuitBreakerState _state = CircuitBreakerState.Closed;
        private readonly object _lock = new();

        public CircuitBreaker(
            int failureThreshold = 5,
            TimeSpan? resetTimeout = null,
            IKernelContext? context = null)
        {
            _failureThreshold = failureThreshold;
            _resetTimeout = resetTimeout ?? TimeSpan.FromMinutes(1);
            _context = context;
        }

        public CircuitBreakerState State => _state;

        /// <summary>
        /// Execute operation through circuit breaker.
        /// </summary>
        public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
        {
            lock (_lock)
            {
                if (_state == CircuitBreakerState.Open)
                {
                    if (DateTime.UtcNow - _lastFailureTime >= _resetTimeout)
                    {
                        _state = CircuitBreakerState.HalfOpen;
                        _context?.LogInfo("[CircuitBreaker] Transitioning to HalfOpen state");
                    }
                    else
                    {
                        throw new CircuitBreakerOpenException("Circuit breaker is OPEN");
                    }
                }
            }

            try
            {
                var result = await operation();

                lock (_lock)
                {
                    if (_state == CircuitBreakerState.HalfOpen)
                    {
                        _state = CircuitBreakerState.Closed;
                        _failureCount = 0;
                        _context?.LogInfo("[CircuitBreaker] Transitioning to Closed state");
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                lock (_lock)
                {
                    _failureCount++;
                    _lastFailureTime = DateTime.UtcNow;

                    if (_failureCount >= _failureThreshold || _state == CircuitBreakerState.HalfOpen)
                    {
                        _state = CircuitBreakerState.Open;
                        _context?.LogWarning($"[CircuitBreaker] Transitioning to Open state (failures: {_failureCount})");
                    }
                }

                throw;
            }
        }

        /// <summary>
        /// Manually reset circuit breaker.
        /// </summary>
        public void Reset()
        {
            lock (_lock)
            {
                _state = CircuitBreakerState.Closed;
                _failureCount = 0;
                _lastFailureTime = null;
                _context?.LogInfo("[CircuitBreaker] Manually reset to Closed state");
            }
        }
    }

    public enum CircuitBreakerState
    {
        Closed,    // Normal operation
        Open,      // Blocking all requests
        HalfOpen   // Testing if service recovered
    }

    public class RetryException : Exception
    {
        public RetryException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    public class CircuitBreakerOpenException : Exception
    {
        public CircuitBreakerOpenException(string message)
            : base(message)
        {
        }
    }
}
