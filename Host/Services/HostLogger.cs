using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text;

namespace SoftwareCenter.Host.Services
{
    /// <summary>
    /// A basic, file-based logger that serves as the default logging implementation.
    /// </summary>
    public class HostLogger : ILogger, IDisposable
    {
        private readonly string _logFilePath;
        private readonly StreamWriter _streamWriter;
        private readonly object _lock = new();

        public HostLogger(string logDirectory)
        {
            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }
            _logFilePath = Path.Combine(logDirectory, $"host-log-{DateTime.Now:yyyy-MM-dd}.log");
            _streamWriter = new StreamWriter(_logFilePath, append: true, Encoding.UTF8) { AutoFlush = true };
        }

        public IDisposable BeginScope<TState>(TState state) => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = new StringBuilder();
            message.Append($"[{DateTime.UtcNow:HH:mm:ss.fff} {logLevel.ToString().ToUpper()}] ");
            message.Append(formatter(state, exception));

            if (exception != null)
            {
                message.AppendLine().Append(exception);
            }

            lock (_lock)
            {
                _streamWriter.WriteLine(message.ToString());
            }
        }

        public void Dispose() => _streamWriter.Dispose();
    }
}

