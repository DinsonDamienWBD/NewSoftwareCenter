using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace DataWarehouse.AI
{
    /// <summary>
    /// V3.0: Hierarchical Graph Index (Simplified HNSW).
    /// Provides O(log N) search speed for Hyperscale datasets.
    /// </summary>
    public class GraphVectorIndex
    {
        private readonly ConcurrentDictionary<string, Node> _nodes = new();
        private string? _entryPointId;
        private readonly ILogger _logger;
        private readonly Random _rng = new();

        // HNSW Constants
        private const int MaxNeighbors = 16;   // M
        private const int MaxLayer = 3;        // Height of the pyramid
        private static readonly double LevelLambda = 1.0 / Math.Log(MaxNeighbors);

        // The Layers: Level 0 is the base (All Nodes). Level 3 is the top (Fewest Nodes).
        private readonly ConcurrentDictionary<string, Node>[] _layers;
        private readonly Lock _writeLock = new();

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logger"></param>
        public GraphVectorIndex(ILogger logger)
        {
            _logger = logger;
            _layers = new ConcurrentDictionary<string, Node>[MaxLayer + 1];
            for (int i = 0; i <= MaxLayer; i++)
                _layers[i] = new ConcurrentDictionary<string, Node>();
        }

        private record Node(string Id, float[] Vector)
        {
            public ConcurrentBag<string> Neighbors { get; } = [];
        }

        /// <summary>
        /// Insert
        /// </summary>
        /// <param name="id"></param>
        /// <param name="vector"></param>
        public void Insert(string id, float[] vector)
        {
            int level = GetRandomLevel();
            var newNode = new Node(id, vector);

            lock (_writeLock)
            {
                if (_entryPointId == null)
                {
                    _entryPointId = id;
                    // Insert into all levels up to determined level
                    for (int i = 0; i <= level; i++) _layers[i][id] = newNode;
                    return;
                }

                // 1. Fleshed Out: Climb from Top Layer down to Insertion Level
                // This finds the closest entry point for the new node's level
                string currObj = _entryPointId;
                for (int lc = MaxLayer; lc > level; lc--)
                {
                    if (!_layers[lc].IsEmpty)
                    {
                        currObj = GreedySearchOneLayer(lc, currObj, vector);
                    }
                }

                // 2. Fleshed Out: Insert and Link downwards
                for (int lc = level; lc >= 0; lc--)
                {
                    _layers[lc][id] = newNode;

                    // Find nearest neighbors in this layer
                    var nearest = GreedySearchOneLayer(lc, currObj, vector);

                    // Bidirectional Connection (Robust NSW)
                    Connect(newNode, _layers[lc][nearest]);

                    // Update current object for next layer down
                    currObj = id;
                }

                // Update Entry Point if new node is higher than current
                if (level > 0 && !_layers[MaxLayer].ContainsKey(_entryPointId))
                {
                    // If we inserted at a level higher than current entry point, update it
                    if (_layers[level].ContainsKey(id)) _entryPointId = id;
                }
            }
        }

        /// <summary>
        /// Search
        /// </summary>
        /// <param name="query"></param>
        /// <param name="limit"></param>
        /// <returns></returns>
        public List<string> Search(float[] query, int limit)
        {
            string? currObj = _entryPointId;
            if (currObj == null) return [];

            // 1. Zoom down hierarchy
            for (int lc = MaxLayer; lc > 0; lc--)
            {
                if (!_layers[lc].IsEmpty)
                    currObj = GreedySearchOneLayer(lc, currObj, query);
            }

            // 2. Base Layer Search
            return SearchLayerZero(currObj, query, limit);
        }

        private string GreedySearchOneLayer(int layer, string entryPoint, float[] target)
        {
            string currentId = entryPoint;
            // [FIX CS8604] Safe dictionary access
            if (!_layers[layer].TryGetValue(currentId, out var currentNode)) return currentId;

            float minDist = VectorMath.Distance(target, currentNode.Vector);
            bool changed = true;

            while (changed)
            {
                changed = false;
                if (!_layers[layer].TryGetValue(currentId, out var node)) break;

                foreach (var neighborId in node.Neighbors)
                {
                    if (_layers[layer].TryGetValue(neighborId, out var neighbor))
                    {
                        float d = VectorMath.Distance(target, neighbor.Vector);
                        if (d < minDist)
                        {
                            minDist = d;
                            currentId = neighborId;
                            changed = true;
                        }
                    }
                }
            }
            return currentId;
        }

        private List<string> SearchLayerZero(string entryPoint, float[] target, int k)
        {
            var candidates = new PriorityQueue<string, float>();
            var visited = new HashSet<string>();
            var results = new List<(string Id, float Dist)>();

            // [FIX CS8604]
            if (!_layers[0].TryGetValue(entryPoint, out Node? value)) return [];

            float initialDist = VectorMath.Distance(target, value.Vector);
            candidates.Enqueue(entryPoint, initialDist);

            while (candidates.Count > 0)
            {
                candidates.TryDequeue(out var currentId, out var dist);
                if (currentId == null || visited.Contains(currentId)) continue;
                visited.Add(currentId);
                results.Add((currentId, dist));

                if (results.Count >= k * 3) break;

                if (_layers[0].TryGetValue(currentId, out var node))
                {
                    foreach (var neighborId in node.Neighbors)
                    {
                        if (!visited.Contains(neighborId))
                        {
                            var neighbor = _layers[0][neighborId];
                            var d = VectorMath.Distance(target, neighbor.Vector);
                            candidates.Enqueue(neighborId, d);
                        }
                    }
                }
            }

            return [.. results.OrderBy(x => x.Dist).Take(k).Select(x => x.Id)];
        }

        // [FIX IDE0060] - Renamed unused params if needed, but here we use them.
        private static void Connect(Node a, Node b)
        {
            if (a.Neighbors.Count < MaxNeighbors) a.Neighbors.Add(b.Id);
            if (b.Neighbors.Count < MaxNeighbors) b.Neighbors.Add(a.Id);
        }

        private int GetRandomLevel()
        {
            int lvl = 0;
            while (_rng.NextDouble() < 0.5 && lvl < MaxLayer) lvl++;
            return lvl;
        }
    }
}