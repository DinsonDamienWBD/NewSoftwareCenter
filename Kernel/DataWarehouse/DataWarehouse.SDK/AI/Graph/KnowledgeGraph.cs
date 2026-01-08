
namespace DataWarehouse.SDK.AI.Graph
{
    /// <summary>
    /// Knowledge graph for managing capabilities, plugins, dependencies, and relationships.
    /// Represents the DataWarehouse system as a directed graph where nodes are capabilities
    /// and edges are relationships (dependencies, flows, compatibility, etc.).
    ///
    /// Used by AI Runtime to:
    /// - Understand capability relationships
    /// - Generate execution plans
    /// - Detect conflicts and cycles
    /// - Find execution paths
    /// - Perform topological sorting
    ///
    /// Graph structure:
    /// - Nodes: Capabilities, plugins, data types
    /// - Edges: Dependencies, flows, compatibility, alternatives
    /// </summary>
    public class KnowledgeGraph
    {
        private readonly Dictionary<string, GraphNode> _nodes = [];
        private readonly List<GraphEdge> _edges = [];
        private readonly Lock _lock = new();

        /// <summary>Gets the total number of nodes in the graph.</summary>
        public int NodeCount
        {
            get { lock (_lock) return _nodes.Count; }
        }

        /// <summary>Gets the total number of edges in the graph.</summary>
        public int EdgeCount
        {
            get { lock (_lock) return _edges.Count; }
        }

        // =========================================================================
        // NODE MANAGEMENT
        // =========================================================================

        /// <summary>
        /// Adds a node to the knowledge graph.
        /// Nodes typically represent capabilities, plugins, or data types.
        /// </summary>
        /// <param name="node">The node to add.</param>
        public void AddNode(GraphNode node)
        {
            ArgumentNullException.ThrowIfNull(node);
            if (string.IsNullOrWhiteSpace(node.Id))
                throw new ArgumentException("Node ID cannot be empty");

            lock (_lock)
            {
                _nodes[node.Id] = node;
            }
        }

        /// <summary>
        /// Removes a node from the graph.
        /// Also removes all edges connected to this node.
        /// </summary>
        /// <param name="nodeId">ID of the node to remove.</param>
        public void RemoveNode(string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
                throw new ArgumentException("Node ID cannot be empty");

            lock (_lock)
            {
                _nodes.Remove(nodeId);

                // Remove edges connected to this node
                _edges.RemoveAll(e => e.SourceId == nodeId || e.TargetId == nodeId);
            }
        }

        /// <summary>
        /// Gets a node by ID.
        /// </summary>
        /// <param name="nodeId">ID of the node.</param>
        /// <returns>The node, or null if not found.</returns>
        public GraphNode? GetNode(string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
                return null;

            lock (_lock)
            {
                return _nodes.TryGetValue(nodeId, out var node) ? node : null;
            }
        }

        /// <summary>
        /// Gets all nodes in the graph.
        /// </summary>
        /// <returns>List of all nodes.</returns>
        public List<GraphNode> GetAllNodes()
        {
            lock (_lock)
            {
                return [.. _nodes.Values];
            }
        }

        /// <summary>
        /// Finds nodes by type.
        /// </summary>
        /// <param name="nodeType">Type of nodes to find (e.g., "capability", "plugin").</param>
        /// <returns>List of matching nodes.</returns>
        public List<GraphNode> GetNodesByType(string nodeType)
        {
            if (string.IsNullOrWhiteSpace(nodeType))
                return [];

            lock (_lock)
            {
                return [.. _nodes.Values.Where(n => n.Type == nodeType)];
            }
        }

        // =========================================================================
        // EDGE MANAGEMENT
        // =========================================================================

        /// <summary>
        /// Adds a directed edge between two nodes.
        /// Edges represent relationships like dependencies, flows, compatibility.
        /// </summary>
        /// <param name="edge">The edge to add.</param>
        public void AddEdge(GraphEdge edge)
        {
            ArgumentNullException.ThrowIfNull(edge);
            if (string.IsNullOrWhiteSpace(edge.SourceId))
                throw new ArgumentException("Source ID cannot be empty");
            if (string.IsNullOrWhiteSpace(edge.TargetId))
                throw new ArgumentException("Target ID cannot be empty");

            lock (_lock)
            {
                // Verify that both nodes exist
                if (!_nodes.ContainsKey(edge.SourceId))
                    throw new ArgumentException($"Source node '{edge.SourceId}' does not exist");
                if (!_nodes.ContainsKey(edge.TargetId))
                    throw new ArgumentException($"Target node '{edge.TargetId}' does not exist");

                _edges.Add(edge);
            }
        }

        /// <summary>
        /// Removes an edge from the graph.
        /// </summary>
        /// <param name="sourceId">Source node ID.</param>
        /// <param name="targetId">Target node ID.</param>
        /// <param name="edgeType">Type of edge to remove (optional, removes all if null).</param>
        public void RemoveEdge(string sourceId, string targetId, string? edgeType = null)
        {
            lock (_lock)
            {
                if (edgeType == null)
                {
                    _edges.RemoveAll(e => e.SourceId == sourceId && e.TargetId == targetId);
                }
                else
                {
                    _edges.RemoveAll(e => e.SourceId == sourceId && e.TargetId == targetId && e.Type == edgeType);
                }
            }
        }

        /// <summary>
        /// Gets all edges in the graph.
        /// </summary>
        /// <returns>List of all edges.</returns>
        public List<GraphEdge> GetAllEdges()
        {
            lock (_lock)
            {
                return [.. _edges];
            }
        }

        /// <summary>
        /// Gets outgoing edges from a node.
        /// </summary>
        /// <param name="nodeId">Source node ID.</param>
        /// <param name="edgeType">Optional edge type filter.</param>
        /// <returns>List of outgoing edges.</returns>
        public List<GraphEdge> GetOutgoingEdges(string nodeId, string? edgeType = null)
        {
            lock (_lock)
            {
                return [.. _edges.Where(e => e.SourceId == nodeId && (edgeType == null || e.Type == edgeType))];
            }
        }

        /// <summary>
        /// Gets incoming edges to a node.
        /// </summary>
        /// <param name="nodeId">Target node ID.</param>
        /// <param name="edgeType">Optional edge type filter.</param>
        /// <returns>List of incoming edges.</returns>
        public List<GraphEdge> GetIncomingEdges(string nodeId, string? edgeType = null)
        {
            lock (_lock)
            {
                return [.. _edges.Where(e => e.TargetId == nodeId && (edgeType == null || e.Type == edgeType))];
            }
        }

        // =========================================================================
        // PATH FINDING
        // =========================================================================

        /// <summary>
        /// Finds all paths from source to target node.
        /// Uses depth-first search with cycle detection.
        ///
        /// Use cases:
        /// - Find execution sequences (compress → encrypt → store)
        /// - Discover capability chains
        /// - Plan multi-step workflows
        /// </summary>
        /// <param name="sourceId">Starting node ID.</param>
        /// <param name="targetId">Ending node ID.</param>
        /// <param name="maxDepth">Maximum path length (default 10).</param>
        /// <returns>List of paths (each path is a list of node IDs).</returns>
        public List<List<string>> FindPaths(string sourceId, string targetId, int maxDepth = 10)
        {
            var paths = new List<List<string>>();
            var currentPath = new List<string>();
            var visited = new HashSet<string>();

            FindPathsRecursive(sourceId, targetId, currentPath, visited, paths, maxDepth);

            return paths;
        }

        private void FindPathsRecursive(
            string currentId,
            string targetId,
            List<string> currentPath,
            HashSet<string> visited,
            List<List<string>> paths,
            int remainingDepth)
        {
            if (remainingDepth < 0)
                return;

            currentPath.Add(currentId);
            visited.Add(currentId);

            if (currentId == targetId)
            {
                paths.Add([.. currentPath]);
            }
            else
            {
                var outgoing = GetOutgoingEdges(currentId);
                foreach (var edge in outgoing)
                {
                    if (!visited.Contains(edge.TargetId))
                    {
                        FindPathsRecursive(edge.TargetId, targetId, currentPath, visited, paths, remainingDepth - 1);
                    }
                }
            }

            currentPath.RemoveAt(currentPath.Count - 1);
            visited.Remove(currentId);
        }

        // =========================================================================
        // CYCLE DETECTION
        // =========================================================================

        /// <summary>
        /// Detects if the graph contains cycles.
        /// Cycles indicate circular dependencies or infinite loops.
        ///
        /// Use cases:
        /// - Validate execution plans
        /// - Detect circular plugin dependencies
        /// - Ensure topological sort is possible
        /// </summary>
        /// <returns>True if cycles exist, false otherwise.</returns>
        public bool HasCycles()
        {
            lock (_lock)
            {
                var visited = new HashSet<string>();
                var recursionStack = new HashSet<string>();

                foreach (var nodeId in _nodes.Keys)
                {
                    if (HasCyclesRecursive(nodeId, visited, recursionStack))
                        return true;
                }

                return false;
            }
        }

        private bool HasCyclesRecursive(string nodeId, HashSet<string> visited, HashSet<string> recursionStack)
        {
            if (recursionStack.Contains(nodeId))
                return true; // Cycle detected

            if (visited.Contains(nodeId))
                return false; // Already checked this subtree

            visited.Add(nodeId);
            recursionStack.Add(nodeId);

            var outgoing = GetOutgoingEdges(nodeId);
            foreach (var edge in outgoing)
            {
                if (HasCyclesRecursive(edge.TargetId, visited, recursionStack))
                    return true;
            }

            recursionStack.Remove(nodeId);
            return false;
        }

        // =========================================================================
        // TOPOLOGICAL SORTING
        // =========================================================================

        /// <summary>
        /// Performs topological sorting on the graph.
        /// Returns nodes in an order where dependencies come before dependents.
        ///
        /// Use cases:
        /// - Determine execution order for capabilities
        /// - Order plugin initialization
        /// - Schedule tasks with dependencies
        ///
        /// Throws exception if graph contains cycles.
        /// </summary>
        /// <returns>List of node IDs in topological order.</returns>
        public List<string> TopologicalSort()
        {
            if (HasCycles())
                throw new InvalidOperationException("Cannot perform topological sort: graph contains cycles");

            lock (_lock)
            {
                var sorted = new List<string>();
                var visited = new HashSet<string>();

                foreach (var nodeId in _nodes.Keys)
                {
                    if (!visited.Contains(nodeId))
                    {
                        TopologicalSortRecursive(nodeId, visited, sorted);
                    }
                }

                sorted.Reverse();
                return sorted;
            }
        }

        private void TopologicalSortRecursive(string nodeId, HashSet<string> visited, List<string> sorted)
        {
            visited.Add(nodeId);

            var outgoing = GetOutgoingEdges(nodeId);
            foreach (var edge in outgoing)
            {
                if (!visited.Contains(edge.TargetId))
                {
                    TopologicalSortRecursive(edge.TargetId, visited, sorted);
                }
            }

            sorted.Add(nodeId);
        }

        // =========================================================================
        // GRAPH QUERIES
        // =========================================================================

        /// <summary>
        /// Finds all dependencies of a node (nodes it depends on).
        /// </summary>
        /// <param name="nodeId">Node to find dependencies for.</param>
        /// <returns>List of dependency node IDs.</returns>
        public List<string> GetDependencies(string nodeId)
        {
            return [.. GetOutgoingEdges(nodeId, "depends_on").Select(e => e.TargetId)];
        }

        /// <summary>
        /// Finds all dependents of a node (nodes that depend on it).
        /// </summary>
        /// <param name="nodeId">Node to find dependents for.</param>
        /// <returns>List of dependent node IDs.</returns>
        public List<string> GetDependents(string nodeId)
        {
            return [.. GetIncomingEdges(nodeId, "depends_on").Select(e => e.SourceId)];
        }

        /// <summary>
        /// Clears the entire graph (removes all nodes and edges).
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _nodes.Clear();
                _edges.Clear();
            }
        }
    }

    /// <summary>
    /// Represents a node in the knowledge graph.
    /// Nodes typically represent capabilities, plugins, or data types.
    /// </summary>
    public class GraphNode
    {
        /// <summary>Unique identifier for this node.</summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>
        /// Type of node (e.g., "capability", "plugin", "datatype").
        /// Used for filtering and querying.
        /// </summary>
        public string Type { get; init; } = string.Empty;

        /// <summary>Human-readable label.</summary>
        public string Label { get; init; } = string.Empty;

        /// <summary>Additional metadata for this node.</summary>
        public Dictionary<string, object> Metadata { get; init; } = [];

        /// <summary>Constructs an empty graph node.</summary>
        public GraphNode() { }

        /// <summary>Constructs a graph node with specified values.</summary>
        public GraphNode(string id, string type, string label)
        {
            Id = id;
            Type = type;
            Label = label;
        }
    }

    /// <summary>
    /// Represents a directed edge in the knowledge graph.
    /// Edges represent relationships between capabilities/plugins.
    /// </summary>
    public class GraphEdge
    {
        /// <summary>Source node ID (edge starts here).</summary>
        public string SourceId { get; init; } = string.Empty;

        /// <summary>Target node ID (edge points here).</summary>
        public string TargetId { get; init; } = string.Empty;

        /// <summary>
        /// Type of relationship.
        /// Examples: "depends_on", "flows_into", "alternative_to", "compatible_with"
        /// </summary>
        public string Type { get; init; } = string.Empty;

        /// <summary>
        /// Weight/strength of the relationship (0.0 to 1.0).
        /// Higher values indicate stronger relationships.
        /// </summary>
        public double Weight { get; init; } = 1.0;

        /// <summary>Additional metadata for this edge.</summary>
        public Dictionary<string, object> Metadata { get; init; } = [];

        /// <summary>Constructs an empty graph edge.</summary>
        public GraphEdge() { }

        /// <summary>Constructs a graph edge with specified values.</summary>
        public GraphEdge(string sourceId, string targetId, string type, double weight = 1.0)
        {
            SourceId = sourceId;
            TargetId = targetId;
            Type = type;
            Weight = weight;
        }
    }
}
