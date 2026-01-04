using DataWarehouse.SDK.Contracts;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace DataWarehouse.Plugins.Features.Governance.Services
{
    /// <summary>
    /// Buffers access timestamps and flushes periodically to avoid IO storms.
    /// </summary>
    public class AccessTracker : IDisposable
    {
        private readonly IMetadataIndex _index;
        private readonly ConcurrentDictionary<string, long> _buffer = new();
        private readonly Timer _flushTimer;
        private readonly ILogger _logger;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="index"></param>
        /// <param name="logger"></param>
        public AccessTracker(IMetadataIndex index, ILogger logger)
        {
            _index = index;
            _logger = logger;
            // Flush every 5 minutes
            _flushTimer = new Timer(FlushAsync, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        }

        /// <summary>
        /// Touch
        /// </summary>
        /// <param name="manifestId"></param>
        public void Touch(string manifestId)
        {
            // O(1) RAM update
            _buffer[manifestId] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        private async void FlushAsync(object? state)
        {
            if (_buffer.IsEmpty) return;

            _logger.LogDebug("[AccessTracker] Flushing access timestamps...");
            var keys = _buffer.Keys.ToArray();

            foreach (var id in keys)
            {
                if (_buffer.TryRemove(id, out var time))
                {
                    try
                    {
                        // [FIXED] Real implementation
                        await _index.UpdateLastAccessAsync(id, time);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Failed to update access time for {id}: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Safely dispose
        /// </summary>
        public void Dispose()
        {
            _flushTimer.Dispose();
            // [FIX CA1816]
            GC.SuppressFinalize(this);
        }
    }
}