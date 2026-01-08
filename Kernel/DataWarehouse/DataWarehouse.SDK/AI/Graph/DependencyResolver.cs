namespace DataWarehouse.SDK.AI.Graph
{
    /// <summary>
    /// Resolves dependencies between capabilities and plugins.
    /// Ensures all required dependencies are available before execution.
    ///
    /// Used by AI Runtime to:
    /// - Check if a capability can execute
    /// - Find missing dependencies
    /// - Suggest required plugins
    /// - Validate plugin loading order
    /// - Detect version conflicts
    ///
    /// Dependency types:
    /// - Hard dependencies: Required for execution (depends_on)
    /// - Soft dependencies: Recommended but optional (compatible_with)
    /// - Conflicts: Cannot coexist (incompatible_with)
    /// </summary>
    /// <remarks>
    /// Constructs a dependency resolver with the given knowledge graph.
    /// </remarks>
    /// <param name="graph">Knowledge graph containing capabilities and dependencies.</param>
    public class DependencyResolver(KnowledgeGraph graph)
    {
        private readonly KnowledgeGraph _graph = graph ?? throw new ArgumentNullException(nameof(graph));

        /// <summary>
        /// Checks if a capability can execute given the current system state.
        /// Verifies all dependencies are satisfied.
        ///
        /// Use cases:
        /// - Before executing a capability
        /// - When generating execution plans
        /// - During plugin loading
        ///
        /// Returns:
        /// - True if all dependencies are met
        /// - False if dependencies are missing
        /// </summary>
        /// <param name="capabilityId">Capability to check.</param>
        /// <returns>True if capability can execute, false otherwise.</returns>
        public bool CanExecute(string capabilityId)
        {
            var result = CheckDependencies(capabilityId);
            return result.AllDependenciesMet;
        }

        /// <summary>
        /// Performs comprehensive dependency check for a capability.
        /// Returns detailed information about satisfied and missing dependencies.
        /// </summary>
        /// <param name="capabilityId">Capability to check.</param>
        /// <returns>Dependency check result with details.</returns>
        public DependencyCheckResult CheckDependencies(string capabilityId)
        {
            if (string.IsNullOrWhiteSpace(capabilityId))
                throw new ArgumentException("Capability ID cannot be empty");

            var node = _graph.GetNode(capabilityId) ?? throw new ArgumentException($"Capability '{capabilityId}' not found");
            var result = new DependencyCheckResult { CapabilityId = capabilityId };

            // Get all "depends_on" edges
            var dependencies = _graph.GetOutgoingEdges(capabilityId, "depends_on");

            foreach (var edge in dependencies)
            {
                var depNode = _graph.GetNode(edge.TargetId);
                if (depNode != null)
                {
                    result.RequiredDependencies.Add(edge.TargetId);

                    // Check if dependency is available (exists in graph)
                    if (IsCapabilityAvailable(edge.TargetId))
                    {
                        result.SatisfiedDependencies.Add(edge.TargetId);
                    }
                    else
                    {
                        result.MissingDependencies.Add(edge.TargetId);
                    }
                }
            }

            result.AllDependenciesMet = result.MissingDependencies.Count == 0;

            return result;
        }

        /// <summary>
        /// Resolves all dependencies for a set of capabilities.
        /// Returns complete list including transitive dependencies.
        ///
        /// Use cases:
        /// - Determine all plugins needed for an execution plan
        /// - Load plugins in correct order
        /// - Display dependency tree to user
        ///
        /// Example:
        /// - Input: ["transform.aes.apply"]
        /// - Output: ["security.keymanager.get", "transform.aes.apply"]
        /// </summary>
        /// <param name="capabilityIds">Capabilities to resolve dependencies for.</param>
        /// <returns>Complete list of capabilities including dependencies (topologically sorted).</returns>
        public List<string> ResolveDependencies(List<string> capabilityIds)
        {
            if (capabilityIds == null || capabilityIds.Count == 0)
                throw new ArgumentException("Capability IDs cannot be empty");

            var resolved = new HashSet<string>();
            var visiting = new HashSet<string>();

            void Resolve(string capId)
            {
                if (resolved.Contains(capId))
                    return;

                if (visiting.Contains(capId))
                    throw new InvalidOperationException($"Circular dependency detected involving '{capId}'");

                visiting.Add(capId);

                // Resolve dependencies first
                var dependencies = _graph.GetOutgoingEdges(capId, "depends_on");
                foreach (var edge in dependencies)
                {
                    Resolve(edge.TargetId);
                }

                visiting.Remove(capId);
                resolved.Add(capId);
            }

            foreach (var capId in capabilityIds)
            {
                Resolve(capId);
            }

            return [.. resolved];
        }

        /// <summary>
        /// Suggests missing plugins needed to satisfy dependencies.
        ///
        /// Use cases:
        /// - User tries to use encryption but key management plugin not loaded
        /// - AI suggests: "To use AES encryption, please load the Key Management plugin"
        /// - Help users understand plugin requirements
        /// </summary>
        /// <param name="capabilityId">Capability to check.</param>
        /// <returns>List of suggested plugin IDs to load.</returns>
        public List<string> SuggestMissingPlugins(string capabilityId)
        {
            var result = CheckDependencies(capabilityId);
            var suggestions = new List<string>();

            foreach (var missingDep in result.MissingDependencies)
            {
                // Find which plugin provides this capability
                var depNode = _graph.GetNode(missingDep);
                if (depNode?.Metadata.TryGetValue("pluginId", out var pluginId) == true)
                {
                    suggestions.Add(pluginId.ToString()!);
                }
            }

            return [.. suggestions.Distinct()];
        }

        /// <summary>
        /// Detects conflicts between capabilities.
        /// Two capabilities conflict if they have "incompatible_with" relationship.
        ///
        /// Use cases:
        /// - Prevent loading conflicting plugins
        /// - Warn user about incompatibilities
        /// - Validate execution plans
        ///
        /// Example conflicts:
        /// - Two consensus algorithms (Raft and Paxos)
        /// - Conflicting encryption modes
        /// </summary>
        /// <param name="capabilityIds">Capabilities to check for conflicts.</param>
        /// <returns>List of detected conflicts.</returns>
        public List<CapabilityConflict> DetectConflicts(List<string> capabilityIds)
        {
            var conflicts = new List<CapabilityConflict>();

            for (int i = 0; i < capabilityIds.Count; i++)
            {
                for (int j = i + 1; j < capabilityIds.Count; j++)
                {
                    var cap1 = capabilityIds[i];
                    var cap2 = capabilityIds[j];

                    // Check if cap1 is incompatible with cap2
                    var incompatibleEdges = _graph.GetOutgoingEdges(cap1, "incompatible_with");
                    var conflictEdge = incompatibleEdges.FirstOrDefault(e => e.TargetId == cap2);

                    if (conflictEdge != null)
                    {
                        var node1 = _graph.GetNode(cap1);
                        var node2 = _graph.GetNode(cap2);

                        conflicts.Add(new CapabilityConflict
                        {
                            Capability1Id = cap1,
                            Capability1Label = node1?.Label ?? cap1,
                            Capability2Id = cap2,
                            Capability2Label = node2?.Label ?? cap2,
                            Reason = conflictEdge.Metadata.TryGetValue("reason", out var reason)
                                ? reason.ToString() ?? "Incompatible capabilities"
                                : "Incompatible capabilities"
                        });
                    }
                }
            }

            return conflicts;
        }

        /// <summary>
        /// Determines optimal loading order for plugins based on dependencies.
        /// Plugins are ordered so dependencies are loaded first.
        ///
        /// Use cases:
        /// - Initialize plugins in correct order
        /// - Avoid "dependency not found" errors
        /// - Ensure system stability
        /// </summary>
        /// <param name="pluginIds">Plugin IDs to order.</param>
        /// <returns>Ordered list of plugin IDs.</returns>
        public List<string> DetermineLoadingOrder(List<string> pluginIds)
        {
            if (pluginIds == null || pluginIds.Count == 0)
                return [];

            // Build subgraph with plugin dependencies
            var pluginGraph = new KnowledgeGraph();

            // Add plugin nodes
            foreach (var pluginId in pluginIds)
            {
                var pluginNode = _graph.GetNodesByType("plugin")
                    .FirstOrDefault(n => n.Id == pluginId);

                if (pluginNode != null)
                {
                    pluginGraph.AddNode(pluginNode);
                }
            }

            // Add dependency edges between plugins
            foreach (var pluginId in pluginIds)
            {
                var dependencies = _graph.GetOutgoingEdges(pluginId, "depends_on");
                foreach (var edge in dependencies)
                {
                    if (pluginIds.Contains(edge.TargetId))
                    {
                        pluginGraph.AddEdge(edge);
                    }
                }
            }

            // Topological sort
            try
            {
                return pluginGraph.TopologicalSort();
            }
            catch
            {
                // If cycles exist, return original order
                return pluginIds;
            }
        }

        /// <summary>
        /// Checks if a capability is currently available in the system.
        /// </summary>
        private bool IsCapabilityAvailable(string capabilityId)
        {
            // Check if capability node exists and is marked as available
            var node = _graph.GetNode(capabilityId);
            if (node == null)
                return false;

            // Check if capability is marked as unavailable in metadata
            if (node.Metadata.TryGetValue("available", out var available))
            {
                return Convert.ToBoolean(available);
            }

            // By default, assume available if node exists
            return true;
        }
    }

    /// <summary>
    /// Result of a dependency check.
    /// </summary>
    public class DependencyCheckResult
    {
        /// <summary>Capability that was checked.</summary>
        public string CapabilityId { get; set; } = string.Empty;

        /// <summary>All required dependencies.</summary>
        public List<string> RequiredDependencies { get; init; } = [];

        /// <summary>Dependencies that are satisfied.</summary>
        public List<string> SatisfiedDependencies { get; init; } = [];

        /// <summary>Dependencies that are missing.</summary>
        public List<string> MissingDependencies { get; init; } = [];

        /// <summary>Whether all dependencies are met.</summary>
        public bool AllDependenciesMet { get; set; }
    }

    /// <summary>
    /// Represents a conflict between two capabilities.
    /// </summary>
    public class CapabilityConflict
    {
        /// <summary>First conflicting capability ID.</summary>
        public string Capability1Id { get; set; } = string.Empty;

        /// <summary>First conflicting capability label.</summary>
        public string Capability1Label { get; set; } = string.Empty;

        /// <summary>Second conflicting capability ID.</summary>
        public string Capability2Id { get; set; } = string.Empty;

        /// <summary>Second conflicting capability label.</summary>
        public string Capability2Label { get; set; } = string.Empty;

        /// <summary>Reason for the conflict.</summary>
        public string Reason { get; set; } = string.Empty;
    }
}
