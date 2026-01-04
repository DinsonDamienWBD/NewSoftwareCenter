using DataWarehouse.SDK.Contracts;
using DataWarehouse.SDK.Governance;
using DataWarehouse.SDK.Primitives;
using DataWarehouse.SDK.Security;

namespace DataWarehouse.Plugins.Features.AI.Modules
{
    /// <summary>
    /// The Immune System of the Data Warehouse.
    /// continuously audits, verifies, and auto-heals data.
    /// </summary>
    public class SentinelDaemon(IKernelContext kernel, INeuralSentinel sentinel)
    {
        private readonly IKernelContext _kernel = kernel;
        private readonly INeuralSentinel _sentinel = sentinel;
        private readonly TimeSpan _scanInterval = TimeSpan.FromMinutes(5);

        public async Task RunAsync(CancellationToken ct)
        {
            _kernel.LogInfo("[SentinelDaemon] Background Guardian Started.");
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var index = _kernel.GetPlugin<IMetadataIndex>();
                    if (index == null) { await Task.Delay(5000, ct); continue; }

                    // Iterate over all files in the system
                    await foreach (var manifest in index.EnumerateAllAsync(ct))
                    {
                        if (ct.IsCancellationRequested) break;

                        // Optimization: Skip if verified today
                        string verificationTag = $"Verified:{DateTime.UtcNow:yyyy-MM-dd}";
                        if (manifest.GovernanceTags.ContainsKey(verificationTag)) continue;

                        await ScanManifestAsync(manifest, index);

                        // Yield to prevent CPU starvation
                        await Task.Delay(50, ct);
                    }

                    // Wait for next cycle
                    await Task.Delay(_scanInterval, ct);
                }
                catch (TaskCanceledException) { break; }
                catch (Exception ex)
                {
                    _kernel.LogError("[SentinelDaemon] Loop Error", ex);
                    await Task.Delay(5000, ct);
                }
            }
        }

        private async Task ScanManifestAsync(Manifest manifest, IMetadataIndex index)
        {
            try
            {
                var sysContext = new SystemSecurityContext();
                Stream? dataStream = null;

                try
                {
                    // Cast Kernel to IDataWarehouse to access storage operations
                    if (_kernel is IDataWarehouse dw)
                    {
                        // Fetch stream (Triggers OnRead checks automatically, but we need the data for Deep Scan)
                        // This handles the decryption pipeline so the Sentinel sees cleartext content
                        dataStream = await dw.GetBlobAsync(sysContext, manifest.ContainerId, GetBlobNameFromUri(manifest.BlobUri));
                    }
                    else
                    {
                        return; // Kernel does not support data access
                    }
                }
                catch (Exception ex)
                {
                    _kernel.LogDebug($"[SentinelDaemon] Read failed for {manifest.Id}: {ex.Message}");
                    // Proceeding to sentinel with null stream might trigger metadata-only checks
                }

                using (dataStream)
                {
                    var context = new SentinelContext
                    {
                        Trigger = TriggerType.OnSchedule,
                        Metadata = manifest,
                        DataStream = dataStream,
                        UserContext = sysContext
                    };

                    // 1. Evaluate
                    var judgment = await _sentinel.EvaluateAsync(context);

                    // 2. Act on Judgment
                    if (judgment.InterventionRequired)
                    {
                        bool metadataUpdated = false;

                        // Log Alerts
                        if (judgment.Alert != null)
                        {
                            _kernel.LogWarning($"[SentinelDaemon] {manifest.Id}: {judgment.Alert.Message}");
                        }

                        // Apply Tags
                        foreach (var tag in judgment.AddTags)
                        {
                            if (!manifest.GovernanceTags.ContainsKey(tag))
                            {
                                manifest.GovernanceTags[tag] = "True";
                                metadataUpdated = true;
                            }
                        }

                        // Apply Property Updates
                        foreach (var prop in judgment.UpdateProperties)
                        {
                            manifest.Tags[prop.Key] = prop.Value;
                            metadataUpdated = true;
                        }

                        // 3. Persist Metadata Changes
                        if (metadataUpdated)
                        {
                            await index.IndexManifestAsync(manifest);
                        }

                        // 4. EXECUTE SELF-HEALING
                        if (!string.IsNullOrEmpty(judgment.HealWithReplicaId))
                        {
                            _kernel.LogInfo($"[SentinelDaemon] Attempting to heal {manifest.Id} from replica '{judgment.HealWithReplicaId}'...");

                            var replicator = _kernel.GetPlugin<IReplicationService>();
                            if (replicator != null)
                            {
                                bool success = await replicator.RestoreAsync(manifest.Id, judgment.HealWithReplicaId);
                                if (success)
                                {
                                    _kernel.LogInfo($"[SentinelDaemon] SUCCESS: {manifest.Id} healed.");

                                    // Update metadata to reflect healthy state
                                    manifest.GovernanceTags.Remove("Status:Corrupt");
                                    manifest.GovernanceTags[$"Verified:{DateTime.UtcNow:yyyy-MM-dd}"] = "True";
                                    await index.IndexManifestAsync(manifest);
                                }
                                else
                                {
                                    _kernel.LogError($"[SentinelDaemon] FAILURE: Could not heal {manifest.Id}.");
                                }
                            }
                            else
                            {
                                _kernel.LogWarning("[SentinelDaemon] Healing requested but no IReplicationService plugin loaded.");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _kernel.LogError($"[SentinelDaemon] Fatal error scanning {manifest.Id}", ex);
            }
        }

        private static string GetBlobNameFromUri(string uri)
        {
            if (Uri.TryCreate(uri, UriKind.Absolute, out var result))
            {
                return result.Segments[^1];
            }
            return uri;
        }

        private class SystemSecurityContext : ISecurityContext
        {
            public string UserId => "SYSTEM_DAEMON";
            public string? TenantId => "SYSTEM";
            public IEnumerable<string> Roles => ["SystemAdmin"];
            public bool IsSystemAdmin => true;
        }
    }
}