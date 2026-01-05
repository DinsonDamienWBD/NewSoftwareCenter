using DataWarehouse.Plugins.Storage.LocalFileSystem.Configuration;
using DataWarehouse.Plugins.Storage.LocalFileSystem.Services; // IStorageEngine
using DataWarehouse.SDK.Contracts;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;

namespace DataWarehouse.Plugins.Storage.LocalFileSystem.Engine
{
    /// <summary>
    /// PLATINUM TIER: Sharded VDI Engine.
    /// Distributes blobs across multiple VDI files (Shards) to bypass OS file size limits
    /// and increase parallel IO throughput.
    /// </summary>
    internal class ShardedStorageEngine : IStorageEngine
    {
        private readonly ConcurrentDictionary<int, VirtualDiskEngine> _shards = new();
        private readonly SortedDictionary<int, int> _ring = []; // Hash -> ShardID
        private readonly int _shardCount;
        private readonly string _rootPath;
        private readonly LocalStorageOptions _options;
        private readonly IKernelContext _context;

        public ShardedStorageEngine(
            string rootPath,
            int shardCount,
            LocalStorageOptions options,
            IKernelContext context)
        {
            _rootPath = rootPath;
            _shardCount = shardCount;
            _options = options;
            _context = context;
            Initialize();
            InitializeRing();
        }

        private void Initialize()
        {
            // Mount shards (Shard_000.vdi, Shard_001.vdi...)
            Parallel.For(0, _shardCount, i =>
            {
                string shardDir = Path.Combine(_rootPath, $"Shard_{i:000}");
                Directory.CreateDirectory(shardDir);

                // Reuse the robust VirtualDiskEngine we built in Phase 2
                var engine = new VirtualDiskEngine(shardDir, _options, _context);
                _shards[i] = engine;
            });
            _context.LogInfo($"[Sharding] Online. {_shardCount} active shards.");
        }

        private void InitializeRing(int virtualNodes = 100)
        {
            // Consistent Hashing Ring Setup
            for (int shardId = 0; shardId < _shardCount; shardId++)
            {
                for (int v = 0; v < virtualNodes; v++)
                {
                    // Create virtual nodes to balance distribution
                    string key = $"SHARD-{shardId}-VN-{v}";
                    int hash = GetStableHash(key);
                    _ring[hash] = shardId;
                }
            }
        }

        private int GetShardId(Uri uri)
        {
            int hash = GetStableHash(uri.ToString());

            // Find first node >= hash (Clockwise search)
            // If not found, wrap around to first node.
            var node = _ring.FirstOrDefault(n => n.Key >= hash);

            if (node.Key == 0 && node.Value == 0) // Default struct check means not found (if hash > all keys)
            {
                return _ring.First().Value; // Wrap around
            }
            return node.Value;
        }

        // FNV-1a Hash (Stable across restarts, unlike GetHashCode)
        private static int GetStableHash(string str)
        {
            unchecked
            {
                const int p = 16777619;
                int hash = (int)2166136261;
                foreach (char c in str)
                    hash = (hash ^ c) * p;
                return hash;
            }
        }

        public Task SaveAsync(Uri uri, Stream data)
        {
            int id = GetShardId(uri);
            return _shards[id].SaveAsync(uri, data);
        }

        public Task<Stream> LoadAsync(Uri uri)
        {
            int id = GetShardId(uri);
            return _shards[id].LoadAsync(uri);
        }

        public Task DeleteAsync(Uri uri)
        {
            int id = GetShardId(uri);
            return _shards[id].DeleteAsync(uri);
        }

        public Task<bool> ExistsAsync(Uri uri)
        {
            int id = GetShardId(uri);
            return _shards[id].ExistsAsync(uri);
        }

        public async Task DisposeAsync()
        {
            foreach (var shard in _shards.Values)
            {
                await shard.DisposeAsync();
            }
        }
    }
}