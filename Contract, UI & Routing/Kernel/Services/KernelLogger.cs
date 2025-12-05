using SoftwareCenter.Core.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace SoftwareCenter.Kernel.Services;

/// <summary>
/// Manages and routes logging requests to registered IScLogger implementations.
/// This service acts as the central "smart router" for all logging, selecting the
/// highest priority logger available. It should be registered as a singleton.
/// </summary>
public class KernelLogger : IScLogger, IDisposable
{
    private readonly List<IScLogger> _loggers = new();
    private readonly ReaderWriterLockSlim _lock = new();
    private volatile IScLogger? _activeLogger; // Volatile for thread-safe reads on the hot path (Log method)

    // The KernelLogger itself is a router; it has no priority.
    public int Priority => int.MinValue;

    public void RegisterLogger(IScLogger logger)
    {
        _lock.EnterWriteLock();
        try
        {
            _loggers.Add(logger);
            UpdateActiveLogger_UNSAFE();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void UnregisterLogger(IScLogger logger)
    {
        _lock.EnterWriteLock();
        try
        {
            if (_loggers.Remove(logger))
            {
                UpdateActiveLogger_UNSAFE();
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    private void UpdateActiveLogger_UNSAFE()
    {
        // This method must be called within a write lock.
        _activeLogger = _loggers.OrderByDescending(x => x.Priority).FirstOrDefault();
    }

    public void Log(LogEntry entry)
    {
        // Reading the volatile field is a thread-safe and highly performant operation.
        _activeLogger?.Log(entry);
    }

    public void Dispose()
    {
        _lock.Dispose();
        GC.SuppressFinalize(this);
    }
}