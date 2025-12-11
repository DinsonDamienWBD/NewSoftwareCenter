using System;
using System.IO;
using Microsoft.Extensions.Logging;

namespace SoftwareCenter.Kernel.Logging
{
    public class FileLogger : ILogger
    {
        private readonly string _filePath;
        private static readonly object _lock = new object();

        public FileLogger(string filePath)
        {
            _filePath = filePath;
        }

        public IDisposable BeginScope<TState>(TState state) => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            lock (_lock)
            {
                var logRecord = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{logLevel}] {formatter(state, exception)}";
                if (exception != null)
                {
                    logRecord += $"{Environment.NewLine}{exception}";
                }

                File.AppendAllText(_filePath, logRecord + Environment.NewLine);
            }
        }
    }
}
