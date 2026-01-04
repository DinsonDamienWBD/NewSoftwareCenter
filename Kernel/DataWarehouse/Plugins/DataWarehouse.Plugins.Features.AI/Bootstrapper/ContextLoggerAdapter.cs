using DataWarehouse.SDK.Contracts;
using Microsoft.Extensions.Logging;

namespace DataWarehouse.Plugins.Features.AI.Bootstrapper
{
    /// <summary>
    /// Bridges the gap between standard .NET ILogger and our Kernel's IKernelContext.
    /// Allows the Engine to use standard logging without depending on the Kernel directly.
    /// </summary>
    /// <typeparam name="T">The category name (usually the class)</typeparam>
    public class ContextLoggerAdapter<T> : ILogger<T>, IDisposable
    {
        private readonly IKernelContext _context;

        /// <summary>
        /// Logger
        /// </summary>
        /// <param name="context"></param>
        public ContextLoggerAdapter(IKernelContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Begin scope
        /// </summary>
        /// <typeparam name="TState"></typeparam>
        /// <param name="state"></param>
        /// <returns></returns>
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => this;

        /// <summary>
        /// Safely dispose
        /// </summary>
        public void Dispose() { } // No-op for scope

        /// <summary>
        /// Is enabled
        /// </summary>
        /// <param name="logLevel"></param>
        /// <returns></returns>
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        /// <summary>
        /// Log
        /// </summary>
        /// <typeparam name="TState"></typeparam>
        /// <param name="logLevel"></param>
        /// <param name="eventId"></param>
        /// <param name="state"></param>
        /// <param name="exception"></param>
        /// <param name="formatter"></param>
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            string message = formatter(state, exception);
            string prefix = $"[{typeof(T).Name}]";

            if (logLevel >= LogLevel.Error)
            {
                _context.LogError($"{prefix} {message}", exception);
            }
            else
            {
                _context.LogInfo($"{prefix} {message}");
            }
        }
    }
}
