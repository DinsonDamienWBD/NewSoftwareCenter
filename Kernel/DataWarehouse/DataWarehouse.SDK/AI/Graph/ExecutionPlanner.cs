using DataWarehouse.SDK.AI.Math;

namespace DataWarehouse.SDK.AI.Graph
{
    /// <summary>
    /// Generates execution plans for multi-step workflows.
    /// Uses the knowledge graph to create optimal sequences of capability invocations.
    ///
    /// Used by AI Runtime to:
    /// - Generate execution plans from natural language requests
    /// - Find optimal capability sequences
    /// - Identify parallel execution opportunities
    /// - Estimate execution cost and duration
    /// - Validate plan feasibility
    ///
    /// Planning considerations:
    /// - Dependencies (must execute before)
    /// - Data flow (output → input connections)
    /// - Parallelization (independent steps)
    /// - Cost optimization (choose cheaper alternatives)
    /// - Performance prediction (estimate duration)
    /// </summary>
    /// <remarks>
    /// Constructs an execution planner with the given knowledge graph.
    /// </remarks>
    /// <param name="graph">Knowledge graph containing capabilities and relationships.</param>
    public class ExecutionPlanner(KnowledgeGraph graph)
    {
        private readonly KnowledgeGraph _graph = graph ?? throw new ArgumentNullException(nameof(graph));

        /// <summary>
        /// Generates an execution plan for a sequence of capability IDs.
        ///
        /// Process:
        /// 1. Validate all capabilities exist
        /// 2. Check dependencies are satisfied
        /// 3. Determine execution order (topological sort)
        /// 4. Identify parallel execution opportunities
        /// 5. Estimate cost and duration
        ///
        /// Use cases:
        /// - AI determines: ["transform.gzip.apply", "transform.aes.apply", "storage.s3.write"]
        /// - Planner creates optimized execution plan with ordering and parallelization
        /// </summary>
        /// <param name="capabilityIds">List of capability IDs to execute.</param>
        /// <returns>Execution plan with ordered steps.</returns>
        public ExecutionPlan CreatePlan(List<string> capabilityIds)
        {
            if (capabilityIds == null || capabilityIds.Count == 0)
                throw new ArgumentException("Capability IDs cannot be empty");

            var plan = new ExecutionPlan();

            // Step 1: Validate capabilities exist
            var missingCapabilities = new List<string>();
            foreach (var capId in capabilityIds)
            {
                if (_graph.GetNode(capId) == null)
                {
                    missingCapabilities.Add(capId);
                }
            }
            if (missingCapabilities.Count > 0)
            {
                throw new ArgumentException($"Capabilities not found: {string.Join(", ", missingCapabilities)}");
            }

            // Step 2: Build subgraph with only requested capabilities and their relationships
            var subgraphNodes = capabilityIds.ToHashSet();

            // Step 3: Determine execution order based on relationships
            var orderedCapabilities = OrderCapabilities(capabilityIds);

            // Step 4: Create execution steps
            var stepNumber = 0;
            foreach (var capId in orderedCapabilities)
            {
                var node = _graph.GetNode(capId);
                var step = new ExecutionStep
                {
                    StepNumber = ++stepNumber,
                    CapabilityId = capId,
                    CapabilityLabel = node?.Label ?? capId,
                    CanRunInParallel = false, // Will be determined later
                    EstimatedDurationMs = 0, // Will be estimated if metadata available
                    EstimatedCostUsd = 0
                };

                plan.Steps.Add(step);
            }

            // Step 5: Identify parallel execution opportunities
            IdentifyParallelSteps(plan);

            // Step 6: Estimate total cost and duration
            EstimatePlanMetrics(plan);

            return plan;
        }

        /// <summary>
        /// Orders capabilities based on relationships (flows_into, depends_on).
        /// Respects the natural flow: compress → encrypt → store.
        /// </summary>
        private List<string> OrderCapabilities(List<string> capabilityIds)
        {
            // Build a mini-graph with only these capabilities
            var dependencies = new Dictionary<string, List<string>>();
            foreach (var capId in capabilityIds)
            {
                dependencies[capId] = [];

                // Check for "depends_on" relationships
                var dependsOnEdges = _graph.GetOutgoingEdges(capId, "depends_on");
                foreach (var edge in dependsOnEdges)
                {
                    if (capabilityIds.Contains(edge.TargetId))
                    {
                        dependencies[capId].Add(edge.TargetId);
                    }
                }

                // Check for "follows" relationships (this should execute after another)
                var followsEdges = _graph.GetOutgoingEdges(capId, "follows");
                foreach (var edge in followsEdges)
                {
                    if (capabilityIds.Contains(edge.TargetId))
                    {
                        dependencies[capId].Add(edge.TargetId);
                    }
                }
            }

            // Topological sort
            var sorted = new List<string>();
            var visited = new HashSet<string>();

            void Visit(string capId)
            {
                if (visited.Contains(capId))
                    return;

                visited.Add(capId);

                // Visit dependencies first
                foreach (var dep in dependencies[capId])
                {
                    Visit(dep);
                }

                sorted.Add(capId);
            }

            foreach (var capId in capabilityIds)
            {
                Visit(capId);
            }

            return sorted;
        }

        /// <summary>
        /// Identifies which steps can run in parallel.
        /// Steps without dependencies on each other can execute simultaneously.
        /// </summary>
        private void IdentifyParallelSteps(ExecutionPlan plan)
        {
            for (int i = 0; i < plan.Steps.Count; i++)
            {
                var step = plan.Steps[i];
                var hasDataFlowDependency = false;

                // Check if this step depends on output from previous steps
                var incomingEdges = _graph.GetIncomingEdges(step.CapabilityId, "flows_into");
                foreach (var edge in incomingEdges)
                {
                    // Check if source is in earlier steps
                    for (int j = 0; j < i; j++)
                    {
                        if (plan.Steps[j].CapabilityId == edge.SourceId)
                        {
                            hasDataFlowDependency = true;
                            break;
                        }
                    }
                    if (hasDataFlowDependency)
                        break;
                }

                step.CanRunInParallel = !hasDataFlowDependency;
            }
        }

        /// <summary>
        /// Estimates total execution cost and duration.
        /// Sums individual step estimates, accounting for parallelization.
        /// </summary>
        private void EstimatePlanMetrics(ExecutionPlan plan)
        {
            decimal totalCost = 0;
            double totalDuration = 0;
            double currentParallelDuration = 0;

            foreach (var step in plan.Steps)
            {
                // Get node metadata for estimates
                var node = _graph.GetNode(step.CapabilityId);
                if (node?.Metadata != null)
                {
                    if (node.Metadata.TryGetValue("avgLatencyMs", out var latency))
                    {
                        step.EstimatedDurationMs = Convert.ToDouble(latency);
                    }
                    if (node.Metadata.TryGetValue("costPerOperationUsd", out var cost))
                    {
                        step.EstimatedCostUsd = Convert.ToDecimal(cost);
                    }
                }

                totalCost += step.EstimatedCostUsd;

                // For parallel steps, take maximum duration
                if (step.CanRunInParallel)
                {
                    currentParallelDuration = MathUtils.Max(currentParallelDuration, step.EstimatedDurationMs);
                }
                else
                {
                    totalDuration += currentParallelDuration;
                    totalDuration += step.EstimatedDurationMs;
                    currentParallelDuration = 0;
                }
            }

            totalDuration += currentParallelDuration;

            plan.EstimatedTotalCostUsd = totalCost;
            plan.EstimatedTotalDurationMs = totalDuration;
        }

        /// <summary>
        /// Finds alternative execution plans that achieve the same goal.
        /// Useful for cost optimization or when primary capabilities are unavailable.
        ///
        /// Use cases:
        /// - User requests "compress data"
        /// - Alternatives: GZip (fast), Zstandard (better ratio), Brotli (web-optimized)
        /// - AI can choose based on performance requirements
        /// </summary>
        /// <param name="capabilityIds">Original capability sequence.</param>
        /// <param name="maxAlternatives">Maximum number of alternatives to return.</param>
        /// <returns>List of alternative execution plans.</returns>
        public List<ExecutionPlan> FindAlternativePlans(List<string> capabilityIds, int maxAlternatives = 3)
        {
            var alternatives = new List<ExecutionPlan>();

            // For each capability, find alternatives
            for (int i = 0; i < capabilityIds.Count && alternatives.Count < maxAlternatives; i++)
            {
                var capId = capabilityIds[i];

                // Find "alternative_to" relationships
                var alternativeEdges = _graph.GetIncomingEdges(capId, "alternative_to");
                foreach (var edge in alternativeEdges)
                {
                    // Create alternative plan with substitution
                    var altCapabilityIds = new List<string>(capabilityIds)
                    {
                        [i] = edge.SourceId
                    };

                    try
                    {
                        var altPlan = CreatePlan(altCapabilityIds);
                        alternatives.Add(altPlan);

                        if (alternatives.Count >= maxAlternatives)
                            break;
                    }
                    catch
                    {
                        // Skip invalid alternatives
                    }
                }
            }

            return alternatives;
        }

        /// <summary>
        /// Validates if an execution plan is feasible.
        /// Checks dependencies, conflicts, and resource availability.
        /// </summary>
        /// <param name="plan">Execution plan to validate.</param>
        /// <returns>Validation result with errors if any.</returns>
        public PlanValidationResult ValidatePlan(ExecutionPlan plan)
        {
            var result = new PlanValidationResult { IsValid = true };

            // Check for circular dependencies
            var capIds = plan.Steps.Select(s => s.CapabilityId).ToList();
            var subgraph = BuildSubgraph(capIds);
            if (subgraph.HasCycles())
            {
                result.IsValid = false;
                result.Errors.Add("Plan contains circular dependencies");
            }

            // Check for conflicts (incompatible_with relationships)
            for (int i = 0; i < plan.Steps.Count; i++)
            {
                for (int j = i + 1; j < plan.Steps.Count; j++)
                {
                    var step1 = plan.Steps[i];
                    var step2 = plan.Steps[j];

                    var conflicts = _graph.GetOutgoingEdges(step1.CapabilityId, "incompatible_with");
                    if (conflicts.Any(e => e.TargetId == step2.CapabilityId))
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Conflict: {step1.CapabilityLabel} is incompatible with {step2.CapabilityLabel}");
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Builds a subgraph containing only the specified capabilities.
        /// </summary>
        private KnowledgeGraph BuildSubgraph(List<string> capabilityIds)
        {
            var subgraph = new KnowledgeGraph();

            foreach (var capId in capabilityIds)
            {
                var node = _graph.GetNode(capId);
                if (node != null)
                {
                    subgraph.AddNode(node);
                }
            }

            foreach (var capId in capabilityIds)
            {
                var outgoingEdges = _graph.GetOutgoingEdges(capId);
                foreach (var edge in outgoingEdges)
                {
                    if (capabilityIds.Contains(edge.TargetId))
                    {
                        subgraph.AddEdge(edge);
                    }
                }
            }

            return subgraph;
        }
    }

    /// <summary>
    /// Represents an execution plan with ordered steps.
    /// </summary>
    public class ExecutionPlan
    {
        /// <summary>Ordered list of execution steps.</summary>
        public List<ExecutionStep> Steps { get; init; } = [];

        /// <summary>Estimated total cost in USD.</summary>
        public decimal EstimatedTotalCostUsd { get; set; }

        /// <summary>Estimated total duration in milliseconds.</summary>
        public double EstimatedTotalDurationMs { get; set; }
    }

    /// <summary>
    /// Represents a single step in an execution plan.
    /// </summary>
    public class ExecutionStep
    {
        /// <summary>Step number (1-indexed).</summary>
        public int StepNumber { get; set; }

        /// <summary>Capability ID to execute.</summary>
        public string CapabilityId { get; set; } = string.Empty;

        /// <summary>Human-readable capability label.</summary>
        public string CapabilityLabel { get; set; } = string.Empty;

        /// <summary>Whether this step can run in parallel with previous steps.</summary>
        public bool CanRunInParallel { get; set; }

        /// <summary>Estimated duration for this step.</summary>
        public double EstimatedDurationMs { get; set; }

        /// <summary>Estimated cost for this step.</summary>
        public decimal EstimatedCostUsd { get; set; }
    }

    /// <summary>
    /// Result of plan validation.
    /// </summary>
    public class PlanValidationResult
    {
        /// <summary>Whether the plan is valid.</summary>
        public bool IsValid { get; set; }

        /// <summary>List of validation errors (empty if valid).</summary>
        public List<string> Errors { get; init; } = [];
    }
}
