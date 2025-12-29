namespace Core.Primitives
{
    /// <summary>
    /// Static Gateway wrapping the standard .NET 8/9 TimeProvider.
    /// Enables standard "Time Travel" testing.
    /// </summary>
    public static class SystemTime
    {
        private static TimeProvider _provider = TimeProvider.System;

        /// <summary>
        /// Gets the current UTC time.
        /// </summary>
        public static DateTimeOffset UtcNow => _provider.GetUtcNow();

        /// <summary>
        /// Gets the current local time.
        /// </summary>
        public static DateTimeOffset LocalNow => _provider.GetLocalNow();

        /// <summary>
        /// Creates a timer using the active provider.
        /// </summary>
        public static ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
            => _provider.CreateTimer(callback, state, dueTime, period);

        /// <summary>
        /// Injects a custom provider (e.g., Microsoft.Extensions.Time.Testing.FakeTimeProvider).
        /// USE FOR UNIT TESTING ONLY.
        /// </summary>
        public static void SetProvider(TimeProvider provider) => _provider = provider;

        /// <summary>
        /// Resets to the system default.
        /// </summary>
        public static void Reset() => _provider = TimeProvider.System;

        // Convenience Wrappers

        /// <summary>
        /// Testable delay in timespan
        /// </summary>
        /// <param name="delay"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public static Task DelayAsync(TimeSpan delay, CancellationToken ct = default)
            => Task.Delay(delay, _provider, ct);

        /// <summary>
        /// Testable Delay in milliseconds
        /// </summary>
        /// <param name="milliseconds"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public static Task DelayAsync(int milliseconds, CancellationToken ct = default)
            => Task.Delay(TimeSpan.FromMilliseconds(milliseconds), _provider, ct);
    }
}