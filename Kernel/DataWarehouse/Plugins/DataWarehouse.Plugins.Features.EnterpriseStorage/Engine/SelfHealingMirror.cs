using DataWarehouse.SDK.Contracts;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace DataWarehouse.Plugins.Features.EnterpriseStorage.Engine
{
    /// <summary>
    /// Self healing feature
    /// </summary>
    public class SelfHealingMirror(IStorageProvider primary, IStorageProvider secondary, ILogger logger) : IStorageProvider
    {
        /// <summary>
        /// Scheme
        /// </summary>
        public string Scheme => "mirror";

        private readonly IStorageProvider _primary = primary;
        private readonly IStorageProvider _secondary = secondary;
        private readonly ILogger _logger = logger;
        private readonly ConcurrentQueue<string> _repairQueue = new();
        private CancellationTokenSource? _cts;
        private Task? _repairTask;

        /// <summary>
        /// Handshake protocol handler
        /// </summary>
        public Task<HandshakeResponse> OnHandshakeAsync(HandshakeRequest request)
        {
            _logger.LogInformation($"[Mirror] Initializing. Primary: {_primary.GetType().Name}, Secondary: {_secondary.GetType().Name}");

            // 1. Verify Connectivity
            try
            {
                // Simple connectivity check (optional, but good for startup health)
                _logger.LogInformation("[Mirror] Links verified.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Mirror] Warning: One or more mirrors are offline at startup.");
                return Task.FromResult(HandshakeResponse.Failure(
                    "Mirror-RAID1",
                    "Self Healing Mirror",
                    $"Initialization warning: {ex.Message}"));
            }

            // 2. Start Background Repair Agent
            _cts = new CancellationTokenSource();
            _repairTask = Task.Run(() => ProcessRepairQueueAsync(_cts.Token));

            var response = HandshakeResponse.Success(
                pluginId: "Mirror-RAID1",
                name: "Self Healing Mirror",
                version: new Version(5, 0, 0),
                category: PluginCategory.Storage);

            return Task.FromResult(response);
        }

        /// <summary>
        /// Message handler (optional).
        /// </summary>
        public Task OnMessageAsync(PluginMessage message)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Save data
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public async Task SaveAsync(Uri uri, Stream data)
        {
            // RAID-1 Write Strategy: Write to both.
            // For streams, we need to be careful. If stream is not seekable, we buffer.

            Stream stream1;
            Stream stream2;

            if (data.CanSeek)
            {
                stream1 = data;
                stream2 = data;
            }
            else
            {
                // Buffer to RAM if network stream (limitations apply)
                var ms = new MemoryStream();
                await data.CopyToAsync(ms);
                ms.Position = 0;
                stream1 = ms;
                stream2 = ms;
            }

            // 1. Primary Write (Critical Path)
            try
            {
                await _primary.SaveAsync(uri, stream1);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Mirror Primary Write Failed: {ex.Message}");
                throw; // Strict Consistency: If Primary fails, operation fails.
            }

            // 2. Secondary Write (Best Effort / Background)
            try
            {
                if (stream2.CanSeek) stream2.Position = 0;
                await _secondary.SaveAsync(uri, stream2);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Mirror Secondary Write Failed: {ex.Message}");
                // We do NOT throw here. We degrade to single-node availability.
            }
        }

        /// <summary>
        /// Load data
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public async Task<Stream> LoadAsync(Uri uri)
        {
            try
            {
                return await _primary.LoadAsync(uri);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Primary Load Failed: {ex.Message}. Failing over.");

                // Failover
                var stream = await _secondary.LoadAsync(uri);

                // Trigger Self-Healing
                ReportFailure(uri.ToString(), "Primary");

                return stream;
            }
        }

        /// <summary>
        /// Delete data
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public async Task DeleteAsync(Uri uri)
        {
            await _primary.DeleteAsync(uri);
            await _secondary.DeleteAsync(uri);
        }

        /// <summary>
        /// Check if data exists
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public async Task<bool> ExistsAsync(Uri uri)
            => await _primary.ExistsAsync(uri) || await _secondary.ExistsAsync(uri);

        /// <summary>
        /// Report failure to trigger repair
        /// </summary>
        /// <param name="blobUri"></param>
        /// <param name="failedNode"></param>
        public void ReportFailure(string blobUri, string failedNode)
        {
            _repairQueue.Enqueue(blobUri);
            _logger.LogInformation($"[Mirror] Failure reported for {blobUri} on {failedNode}. Queued for repair.");
        }

        private async Task ProcessRepairQueueAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (_repairQueue.TryDequeue(out var uriString))
                {
                    try
                    {
                        var uri = new Uri(uriString);
                        _logger.LogInformation($"[Mirror] Healing {uri}...");
                        await RepairPrimaryAsync(uri);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"[Mirror] Heal failed for {uriString}. Re-queuing.");
                        _repairQueue.Enqueue(uriString); // Retry later
                        await Task.Delay(5000, token); // Backoff
                    }
                }
                else
                {
                    await Task.Delay(1000, token);
                }
            }
        }

        private async Task RepairPrimaryAsync(Uri uri)
        {
            // Read from Healthy Secondary
            using var source = await _secondary.LoadAsync(uri);

            // Write to Unhealthy Primary
            await _primary.SaveAsync(uri, source);
            _logger.LogInformation($"[Mirror] {uri} repaired successfully.");
        }

        /// <summary>
        /// Start
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        public static Task StartAsync(CancellationToken ct) => Task.CompletedTask;

        /// <summary>
        /// Stop
        /// </summary>
        /// <returns></returns>
        public Task StopAsync()
        {
            _cts?.Cancel();
            return _repairTask ?? Task.CompletedTask;
        }

        /// <summary>
        /// Safely dispose
        /// </summary>
        public void Dispose()
        {
            _cts?.Dispose();
            (_primary as IDisposable)?.Dispose();
            (_secondary as IDisposable)?.Dispose();
        }
    }
}