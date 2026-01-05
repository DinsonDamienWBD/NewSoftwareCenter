using DataWarehouse.Plugins.Features.EnterpriseStorage.Engine;
using DataWarehouse.SDK.Contracts;
using DataWarehouse.SDK.Primitives;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace DataWarehouse.Plugins.Features.EnterpriseStorage.Services
{
    /// <summary>
    /// The Hypervisor. It turns multiple physical drives/providers into one massive, tiered storage pool.
    /// Manages Tiering, Mirroring, and Deduplication transparently.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the UnifiedStoragePool.
    /// </remarks>
    /// <param name="context">The kernel context for logging and resource access.</param>
    public class UnifiedStoragePool(IKernelContext context)
    {
        /// <summary>
        /// The unique ID of this service.
        /// </summary>
        public static string Id => "UnifiedStoragePool";

        /// <summary>
        /// The service version.
        /// </summary>
        public static string Version => "5.0.0";

        // Internal list of wrapped nodes
        private readonly List<StorageNode> _nodes = [];
        private readonly IKernelContext _context = context;

        /// <summary>
        /// Gets a value indicating whether the pool consists of a single node (Optimization flag).
        /// </summary>
        public bool IsSingleNode => _nodes.Count == 1;

        /// <summary>
        /// Registers a raw storage provider into the Warm tier.
        /// </summary>
        /// <param name="provider">The storage provider instance.</param>
        public void RegisterNode(IStorageProvider provider)
        {
            AttachNode(provider, StorageTier.Warm);
        }

        /// <summary>
        /// Attaches a storage provider to a specific performance tier.
        /// </summary>
        /// <param name="provider">The storage provider.</param>
        /// <param name="tier">The target tier (Hot, Warm, Cold).</param>
        public void AttachNode(IStorageProvider provider, StorageTier tier)
        {
            _nodes.Add(new StorageNode(provider, tier));
            _context.LogInfo($"Attached {provider.Scheme} to Tier {tier}");
        }

        /// <summary>
        /// Attaches two providers as a RAID-1 Mirror pair to a specific tier.
        /// </summary>
        /// <param name="primary">The primary provider (Critical path).</param>
        /// <param name="secondary">The secondary provider (Failover/Recovery).</param>
        /// <param name="tier">The target tier.</param>
        public void AttachMirroredNode(IStorageProvider primary, IStorageProvider secondary, StorageTier tier)
        {
            // Create a Logger Adapter to bridge Kernel Logging to the ILogger required by SelfHealingMirror
            var logger = new ContextLoggerAdapter<SelfHealingMirror>(_context);
            var mirror = new SelfHealingMirror(primary, secondary, logger);

            _nodes.Add(new StorageNode(mirror, tier));
            _context.LogInfo($"Attached MIRROR ({primary.Id}+{secondary.Id}) to Tier {tier}");
        }

        /// <summary>
        /// writes data directly to the first available node, bypassing tiering logic.
        /// </summary>
        /// <param name="data">The data stream.</param>
        /// <returns>The resulting URI.</returns>
        public async Task<string> WriteDirectAsync(Stream data)
        {
            var node = _nodes[0];
            string id = Guid.NewGuid().ToString("N");
            var uri = new Uri($"{node.Provider.Scheme}://pool/{id}");

            await node.Provider.SaveAsync(uri, data);
            return uri.ToString();
        }

        /// <summary>
        /// Intelligently writes a blob to the correct tier based on the Storage Intent (SLA).
        /// Handles Hashing, Deduplication, and Staging.
        /// </summary>
        /// <param name="data">The data stream.</param>
        /// <param name="intent">The SLA definition (Compression, Availability).</param>
        /// <returns>The final URI of the stored blob.</returns>
        public async Task<Uri> WriteBlobAsync(Stream data, StorageIntent intent)
        {
            // 1. Select Node based on Intent (SLA Matching)
            StorageTier targetTier = SelectTier(intent);

            var node = _nodes.FirstOrDefault(n => n.Tier == targetTier)
                       ?? _nodes.FirstOrDefault()
                       ?? throw new InvalidOperationException("No storage nodes available.");

            // 2. Setup Hashing (Calculate SHA256 while streaming)
            using var hasher = SHA256.Create();
            using var hashingStream = new HashingReadStream(data, hasher);

            // 3. Write to Staging Area
            var tempId = Guid.NewGuid().ToString("N");
            var tempUri = new Uri($"{node.Provider.Scheme}://staging/{tempId}");

            await node.Provider.SaveAsync(tempUri, hashingStream);

            // 4. Finalize Hash
            var hashBytes = (hasher.Hash ?? hashingStream.FinalHash) ?? throw new InvalidOperationException("Hash failed to compute.");
            var hash = Convert.ToHexStringLower(hashBytes);
            var finalUri = new Uri($"{node.Provider.Scheme}://pool/{hash}");

            // 5. Deduplication Check
            if (await node.Provider.ExistsAsync(finalUri))
            {
                // Content exists; delete duplicate upload
                await node.Provider.DeleteAsync(tempUri);
                _context.LogInfo($"[Dedup] Discarded duplicate: {hash}");
            }
            else
            {
                // New content; promote from Staging to Pool
                await MoveBlobAsync(node.Provider, tempUri, finalUri);
                _context.LogInfo($"[Write] Stored: {hash}");
            }

            return finalUri;
        }

        /// <summary>
        /// Moves a blob from its current location to a different tier (e.g., Warm -> Cold).
        /// </summary>
        /// <param name="manifest">The blob manifest.</param>
        /// <param name="targetTier">The destination tier.</param>
        /// <returns>The new URI string.</returns>
        public async Task<string> MoveToTierAsync(Manifest manifest, StorageTier targetTier)
        {
            if (!Uri.TryCreate(manifest.BlobUri, UriKind.Absolute, out var sourceUri))
                throw new ArgumentException("Invalid Manifest URI");

            // Find source node by Scheme match
            var sourceNode = _nodes.FirstOrDefault(n => n.Provider.Scheme.Equals(sourceUri.Scheme, StringComparison.OrdinalIgnoreCase))
                             ?? throw new InvalidOperationException($"Source provider '{sourceUri.Scheme}' not found.");

            var targetNode = _nodes.FirstOrDefault(n => n.Tier == targetTier);

            // If already on tier or no target exists, no-op
            if (targetNode == null || sourceNode == targetNode) return manifest.BlobUri;

            // Construct target URI
            var relativePath = sourceUri.AbsolutePath.TrimStart('/');
            var targetUri = new Uri($"{targetNode.Provider.Scheme}://{relativePath}");

            _context.LogInfo($"[Tiering] Moving {manifest.Id} {sourceNode.Tier}->{targetTier}");

            // Perform Move (Copy + Delete)
            using (var stream = await sourceNode.Provider.LoadAsync(sourceUri))
            {
                await targetNode.Provider.SaveAsync(targetUri, stream);
            }

            if (await targetNode.Provider.ExistsAsync(targetUri))
            {
                await sourceNode.Provider.DeleteAsync(sourceUri);
                return targetUri.ToString();
            }
            throw new IOException("Tier move failed.");
        }

        private static StorageTier SelectTier(StorageIntent intent)
        {
            if (intent.Compression == CompressionLevel.None && intent.Availability == AvailabilityLevel.Single)
                return StorageTier.Hot;

            // If GeoRedundant -> COLD (Archive/Offsite)
            if (intent.Availability == AvailabilityLevel.GeoRedundant)
                return StorageTier.Cold;

            // Default -> WARM
            return StorageTier.Warm;
        }

        private static async Task MoveBlobAsync(IStorageProvider provider, Uri src, Uri dest)
        {
            using (var stream = await provider.LoadAsync(src))
            {
                await provider.SaveAsync(dest, stream);
            }
            await provider.DeleteAsync(src);
        }

        // --- Internal Wrappers ---

        internal class StorageNode(IStorageProvider provider, StorageTier tier)
        {
            public IStorageProvider Provider { get; } = provider;
            public StorageTier Tier { get; } = tier;
        }

        private class HashingReadStream(Stream inner, HashAlgorithm hasher) : Stream
        {
            private readonly Stream _inner = inner;
            private readonly CryptoStream _cryptoStream = new(Stream.Null, hasher, CryptoStreamMode.Write);
            public byte[]? FinalHash { get; private set; }
            private readonly HashAlgorithm _hasher = hasher;

            public override int Read(byte[] buffer, int offset, int count)
            {
                int read = _inner.Read(buffer, offset, count);
                if (read > 0)
                {
                    _cryptoStream.Write(buffer, offset, read);
                }
                else
                {
                    if (!_cryptoStream.HasFlushedFinalBlock)
                    {
                        _cryptoStream.FlushFinalBlock();
                        FinalHash = _hasher.Hash;
                    }
                }
                return read;
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => _inner.Length;
            public override long Position { get => _inner.Position; set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            protected override void Dispose(bool disposing)
            {
                if (disposing) _cryptoStream.Dispose();
                base.Dispose(disposing);
            }
        }

        /// <summary>
        /// Adapts the IKernelContext to the standard ILogger interface.
        /// Fixed: Removed IDisposable as this adapter holds no unmanaged resources.
        /// </summary>
        /// <typeparam name="T">The category type.</typeparam>
        public class ContextLoggerAdapter<T>(IKernelContext ctx) : ILogger<T>
        {
            private readonly IKernelContext _ctx = ctx;

            // Solves CA1816 by returning a static singleton instead of 'this'
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull
                => NullScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                // Bridge to Kernel Logging
                string msg = formatter(state, exception);

                if (logLevel >= LogLevel.Error)
                    _ctx.LogError(msg, exception);
                else
                    _ctx.LogInfo(msg);
            }

            // A lightweight, reusable scope that does nothing when disposed
            private sealed class NullScope : IDisposable
            {
                public static readonly NullScope Instance = new();
                public void Dispose() { }
            }
        }
    }
}