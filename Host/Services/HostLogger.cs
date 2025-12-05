using SoftwareCenter.Core.Logging;
using System.Text;

namespace SoftwareCenter.Host.Services;

/// <summary>
/// A basic, file-based logger that serves as the default logging implementation.
/// It has a low priority so that it can be overridden by more advanced logging modules.
/// </summary>
public class HostLogger : IScLogger, IDisposable
{
    private readonly string _logFilePath;
    private readonly StreamWriter _streamWriter;
    private readonly object _lock = new();

    /// <summary>
    /// The priority of this logger. It is set to 0 to be the default.
    /// Higher numbers will take precedence.
    /// </summary>
    public int Priority => 0;

    public HostLogger(string logDirectory)
    {
        if (!Directory.Exists(logDirectory))
        {
            Directory.CreateDirectory(logDirectory);
        }
        _logFilePath = Path.Combine(logDirectory, $"host-log-{DateTime.Now:yyyy-MM-dd}.log");
        _streamWriter = new StreamWriter(_logFilePath, append: true, Encoding.UTF8) { AutoFlush = true };
    }

    /// <summary>
    /// Logs a message and exception (if present) to a local file.
    /// It ignores advanced payloads like the 'State' dictionary.
    /// </summary>
    public void Log(LogEntry entry)
    {
        var message = new StringBuilder();
        message.Append($"[{DateTime.UtcNow:HH:mm:ss.fff} {entry.Level.ToString().ToUpper()}] ");
        message.Append(entry.Message);

        if (entry.Exception != null)
        {
            message.AppendLine().Append(entry.Exception);
        }

        lock (_lock) { _streamWriter.WriteLine(message.ToString()); }
    }

    public void Dispose() => _streamWriter.Dispose();
}