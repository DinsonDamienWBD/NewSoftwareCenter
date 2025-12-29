using Core.Log; // Contains your custom LogLevel (Trace, Info, etc.)
using Core.Backend.Messages;
using Microsoft.Extensions.Logging; // Contains ILogger, [LoggerMessage]

#pragma warning disable CA1873 // suppression: "IsEnabled" guards are manually implemented below

namespace Host.Services
{
    /// <summary>
    /// Adapter 1: SmartLogger -> Microsoft ILogger
    /// </summary>
    /// <param name="logger"></param>
    public partial class HostSmartLogger(ILogger<HostSmartLogger> logger) : ISmartLogger
    {
        private readonly ILogger<HostSmartLogger> _logger = logger;

        /// <summary>
        /// Log an informational message
        /// </summary>
        /// <param name="message"></param>
        /// <param name="context"></param>
        /// <param name="source"></param>
        public void LogInfo(string message, Dictionary<string, object>? context = null, string source = "System")
        {
            // Fix: Use fully qualified Microsoft enum
            if (!_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information)) return;
            LogInfoGen(source, message, FormatContext(context));
        }

        /// <summary>
        /// Log a warning message
        /// </summary>
        /// <param name="message"></param>
        /// <param name="ex"></param>
        /// <param name="context"></param>
        /// <param name="source"></param>
        public void LogWarning(string message, Exception? ex = null, Dictionary<string, object>? context = null, string source = "System")
        {
            if (!_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Warning)) return;
            LogWarningGen(ex, source, message, FormatContext(context));
        }

        /// <summary>
        /// Log an error message
        /// </summary>
        /// <param name="message"></param>
        /// <param name="ex"></param>
        /// <param name="context"></param>
        /// <param name="source"></param>
        public void LogError(string message, Exception? ex = null, Dictionary<string, object>? context = null, string source = "System")
        {
            if (!_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Error)) return;
            LogErrorGen(ex, source, message, FormatContext(context));
        }

        /// <summary>
        /// Log a debug message
        /// </summary>
        /// <param name="message"></param>
        /// <param name="context"></param>
        /// <param name="source"></param>
        public void LogDebug(string message, Dictionary<string, object>? context = null, string source = "System")
        {
            if (!_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Debug)) return;
            LogDebugGen(source, message, FormatContext(context));
        }

        /// <summary>
        /// Log a message from a LogMessageCommand
        /// </summary>
        /// <param name="command"></param>
        public void Log(LogMessageCommand command)
        {
            var msg = command.Message ?? "";
            var src = command.SourceModuleId ?? "System";
            var ctx = FormatContext(command.Properties);
            var ex = command.Exception;

            // Fix: Map Core.Log.LogLevel -> Microsoft.Extensions.Logging.LogLevel
            switch (command.Level)
            {
                case Core.Log.LogLevel.Fatal:
                    if (_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Critical))
                        LogErrorGen(ex, src, msg, ctx);
                    break;
                case Core.Log.LogLevel.Error:
                    if (_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Error))
                        LogErrorGen(ex, src, msg, ctx);
                    break;
                case Core.Log.LogLevel.Warning:
                    if (_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Warning))
                        LogWarningGen(ex, src, msg, ctx);
                    break;
                case Core.Log.LogLevel.Debug:
                case Core.Log.LogLevel.Trace:
                    if (_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Debug))
                        LogDebugGen(src, msg, ctx);
                    break;
                default: // Info
                    if (_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information))
                        LogInfoGen(src, msg, ctx);
                    break;
            }
        }

        // --- Source Generated Methods ---
        // Fix: The 'Level' property MUST use the Microsoft Enum

        [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "[{Source}] {Message} {ContextStr}")]
        private partial void LogInfoGen(string source, string message, string contextStr);

        [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Warning, Message = "[{Source}] {Message} {ContextStr}")]
        private partial void LogWarningGen(Exception? ex, string source, string message, string contextStr);

        [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Error, Message = "[{Source}] {Message} {ContextStr}")]
        private partial void LogErrorGen(Exception? ex, string source, string message, string contextStr);

        [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Debug, Message = "[{Source}] {Message} {ContextStr}")]
        private partial void LogDebugGen(string source, string message, string contextStr);

        private static string FormatContext(Dictionary<string, object>? context)
        {
            if (context == null || context.Count == 0) return string.Empty;
            return "| Data: " + string.Join(", ", context.Select(kv => $"{kv.Key}={kv.Value}"));
        }
    }

    /// <summary>
    /// Adapter 2: AuditLogger -> Microsoft ILogger
    /// </summary>
    /// <param name="logger"></param>
    public partial class HostAuditLogger(ILogger<HostAuditLogger> logger) : IAuditLogger
    {
        private readonly ILogger<HostAuditLogger> _logger = logger;

        /// <summary>
        /// Log an execution event
        /// </summary>
        /// <param name="commandName"></param>
        /// <param name="user"></param>
        /// <param name="details"></param>
        /// <returns></returns>
        public Task LogExecutionAsync(string commandName, string user, string details)
        {
            LogAudit(user, commandName, details);
            return Task.CompletedTask;
        }

        [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "AUDIT | {User} ran {Command}: {Details}")]
        private partial void LogAudit(string user, string command, string details);
    }
}