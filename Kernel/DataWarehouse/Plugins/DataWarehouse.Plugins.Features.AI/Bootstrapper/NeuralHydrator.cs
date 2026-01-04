using DataWarehouse.Plugins.Features.AI.Engine;
using DataWarehouse.SDK.Contracts;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace DataWarehouse.Plugins.Features.AI.Bootstrapper
{
    /// <summary>
    /// Neural Hydrator
    /// </summary>
    public class NeuralHydrator
    {
        private readonly GraphVectorIndex _index;
        private readonly IKernelContext _context;
        private readonly string _persistencePath; 
        private readonly Lock _lock = new();

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="index"></param>
        /// <param name="context"></param>
        public NeuralHydrator(GraphVectorIndex index, IKernelContext context)
        {
            _index = index;
            _context = context;

            string dataDir = Path.Combine(context.RootPath, "NeuralData");
            Directory.CreateDirectory(dataDir);
            _persistencePath = Path.Combine(dataDir, "vectors.bin");
        }

        /// <summary>
        /// Hydrate async
        /// </summary>
        /// <returns></returns>
        public async Task HydrateAsync(CancellationToken ct)
        {
            // Early exit if cancellation requested before we start
            if (ct.IsCancellationRequested) return;

            lock (_lock)
            {
                if (!File.Exists(_persistencePath))
                {
                    _context.LogInfo("[NeuralHydrator] No existing graph found. Starting fresh.");
                    return;
                }
            }

            try
            {
                _context.LogInfo($"[NeuralHydrator] Loading graph from {_persistencePath}...");

                // Use SequentialScan for faster sequential read access
                using var fs = new FileStream(_persistencePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);

                // Delegate to Engine's Binary Deserializer
                await _index.LoadFromStreamAsync(fs);

                _context.LogInfo($"[NeuralHydrator] Graph loaded successfully. Nodes: {_index.Count}");
            }
            catch (Exception ex)
            {
                _context.LogError("[NeuralHydrator] Failed to hydrate graph. File may be corrupted.", ex);

                // Production Safety: Rename corrupted file so we don't loop-crash on next boot
                string backupPath = _persistencePath + ".corrupt." + DateTime.UtcNow.Ticks;
                File.Move(_persistencePath, backupPath);
                _context.LogInfo($"[NeuralHydrator] Corrupted graph moved to {backupPath}. Starting fresh.");
            }
        }

        /// <summary>
        /// Added for Production Completeness: The method to call on Shutdown
        /// </summary>
        /// <returns></returns>
        public async Task SaveAsync()
        {
            try
            {
                _context.LogInfo($"[NeuralHydrator] Persisting graph to {_persistencePath}...");

                // Atomic Write Strategy: Write to .tmp, then Move
                string tempPath = _persistencePath + ".tmp";

                using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096))
                {
                    await _index.SaveToStreamAsync(fs);
                }

                // Atomic Swap
                File.Move(tempPath, _persistencePath, overwrite: true);

                _context.LogInfo("[NeuralHydrator] Graph persisted successfully.");
            }
            catch (Exception ex)
            {
                _context.LogError("[NeuralHydrator] Failed to persist graph.", ex);
            }
        }
    }
}
