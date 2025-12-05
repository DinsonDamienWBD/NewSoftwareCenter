using System.ComponentModel;

namespace SoftwareCenter.Core.Logging;

/// <summary>
/// Defines the contract for a logger within the SoftwareCenter ecosystem.
/// Implementations of this interface can be registered with the Kernel to handle logging requests.
/// </summary>
public interface IScLogger
{
    void Log(LogEntry entry);

    int Priority { get; }
}