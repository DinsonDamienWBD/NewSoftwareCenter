using System;
using System.Collections.Generic;
using System.Linq;
using DataWarehouse.SDK.AI.Graph;
using DataWarehouse.SDK.Contracts;

namespace DataWarehouse.SDK.AI.Math
{
    /// <summary>
    /// Optimizes execution plans for cost, speed, or reliability.
    /// Compares alternative approaches and selects the best based on optimization criteria.
    ///
    /// Used by AI Runtime to:
    /// - Choose cheapest execution plan
    /// - Choose fastest execution plan
    /// - Choose most reliable execution plan
    /// - Balance multiple objectives (cost vs speed)
    /// - Compare alternative capabilities
    ///
    /// Optimization objectives:
    /// - MinimizeCost: Choose lowest monetary cost
    /// - MinimizeDuration: Choose fastest execution
    /// - MaximizeReliability: Choose most stable/reliable
    /// - BalancedEfficiency: Optimize cost-to-performance ratio
    /// </summary>
    public class CostOptimizer
    {
        private readonly KnowledgeGraph _graph;

        /// <summary>
        /// Constructs a cost optimizer with the given knowledge graph.
        /// </summary>
        /// <param name="graph">Knowledge graph containing capability performance data.</param>
        public CostOptimizer(KnowledgeGraph graph)
        {
            _graph = graph ?? throw new ArgumentNullException(nameof(graph));
        }

        /// <summary>
        /// Compares multiple execution plans and selects the best based on objective.
        ///
        /// Use cases:
        /// - User: "Compress my data as cheaply as possible"
        /// - Optimizer evaluates GZip vs Zstandard vs Brotli
        /// - Returns cheapest option (GZip - free CPU compression)
        ///
        /// - User: "Process this urgently"
        /// - Optimizer chooses fastest compression (GZip level=fastest)
        /// </summary>
        /// <param name="plans">List of alternative execution plans.</param>
        /// <param name="objective">Optimization objective.</param>
        /// <returns>The optimal execution plan.</returns>
        public ExecutionPlan SelectOptimalPlan(List<ExecutionPlan> plans, OptimizationObjective objective)
        {
            if (plans == null || plans.Count == 0)
                throw new ArgumentException("Plans cannot be empty");

            return objective switch
            {
                OptimizationObjective.MinimizeCost => plans.OrderBy(p => p.EstimatedTotalCostUsd).First(),
                OptimizationObjective.MinimizeDuration => plans.OrderBy(p => p.EstimatedTotalDurationMs).First(),
                OptimizationObjective.MaximizeReliability => SelectMostReliablePlan(plans),
                OptimizationObjective.BalancedEfficiency => SelectBalancedPlan(plans),
                _ => plans.First()
            };
        }

        /// <summary>
        /// Calculates the cost-to-performance ratio for a plan.
        /// Lower values are better (more performance per dollar).
        ///
        /// Formula: Cost (USD) / (1 / Duration (seconds))
        /// Result: Cost per operation per second
        /// </summary>
        /// <param name="plan">Execution plan to evaluate.</param>
        /// <returns>Cost-to-performance ratio.</returns>
        public double CalculateCostPerformanceRatio(ExecutionPlan plan)
        {
            if (plan.EstimatedTotalDurationMs == 0)
                return plan.EstimatedTotalCostUsd == 0 ? 0 : double.MaxValue;

            var durationSeconds = plan.EstimatedTotalDurationMs / 1000.0;
            var operationsPerSecond = 1.0 / durationSeconds;

            if (operationsPerSecond == 0)
                return double.MaxValue;

            return (double)plan.EstimatedTotalCostUsd / operationsPerSecond;
        }

        /// <summary>
        /// Finds the cheapest alternative for a specific capability.
        ///
        /// Use cases:
        /// - AI needs compression
        /// - Alternatives: GZip ($0), Zstandard ($0), Commercial algorithm ($0.001)
        /// - Returns: GZip or Zstandard (both free)
        /// </summary>
        /// <param name="capabilityId">Capability to find alternatives for.</param>
        /// <returns>Cheapest alternative capability ID.</returns>
        public string? FindCheapestAlternative(string capabilityId)
        {
            var alternatives = FindAlternatives(capabilityId);
            if (alternatives.Count == 0)
                return capabilityId; // No alternatives

            var cheapest = alternatives
                .OrderBy(alt => GetCapabilityCost(alt))
                .FirstOrDefault();

            return cheapest ?? capabilityId;
        }

        /// <summary>
        /// Finds the fastest alternative for a specific capability.
        ///
        /// Use cases:
        /// - AI needs storage
        /// - Alternatives: Local disk (50ms), SSD (10ms), RAM (1ms), S3 (200ms)
        /// - Returns: RAM storage
        /// </summary>
        /// <param name="capabilityId">Capability to find alternatives for.</param>
        /// <returns>Fastest alternative capability ID.</returns>
        public string? FindFastestAlternative(string capabilityId)
        {
            var alternatives = FindAlternatives(capabilityId);
            if (alternatives.Count == 0)
                return capabilityId; // No alternatives

            var fastest = alternatives
                .OrderBy(alt => GetCapabilityDuration(alt))
                .FirstOrDefault();

            return fastest ?? capabilityId;
        }

        /// <summary>
        /// Estimates cost savings from optimizing an execution plan.
        ///
        /// Use cases:
        /// - Show user potential savings
        /// - Justify using alternative capabilities
        /// - Report on optimization effectiveness
        /// </summary>
        /// <param name="originalPlan">Original execution plan.</param>
        /// <param name="optimizedPlan">Optimized execution plan.</param>
        /// <returns>Cost savings result.</returns>
        public CostSavingsResult CalculateSavings(ExecutionPlan originalPlan, ExecutionPlan optimizedPlan)
        {
            var costSavings = originalPlan.EstimatedTotalCostUsd - optimizedPlan.EstimatedTotalCostUsd;
            var durationSavings = originalPlan.EstimatedTotalDurationMs - optimizedPlan.EstimatedTotalDurationMs;

            var costSavingsPercent = originalPlan.EstimatedTotalCostUsd > 0
                ? (costSavings / originalPlan.EstimatedTotalCostUsd) * 100
                : 0;

            var durationSavingsPercent = originalPlan.EstimatedTotalDurationMs > 0
                ? (durationSavings / originalPlan.EstimatedTotalDurationMs) * 100
                : 0;

            return new CostSavingsResult
            {
                CostSavingsUsd = costSavings,
                CostSavingsPercent = (double)costSavingsPercent,
                DurationSavingsMs = durationSavings,
                DurationSavingsPercent = durationSavingsPercent
            };
        }

        /// <summary>
        /// Selects the most reliable plan based on reliability scores.
        /// </summary>
        private ExecutionPlan SelectMostReliablePlan(List<ExecutionPlan> plans)
        {
            return plans
                .Select(plan => new
                {
                    Plan = plan,
                    ReliabilityScore = CalculatePlanReliability(plan)
                })
                .OrderByDescending(x => x.ReliabilityScore)
                .First()
                .Plan;
        }

        /// <summary>
        /// Calculates overall reliability score for a plan.
        /// Reliability is product of individual step reliabilities.
        /// </summary>
        private double CalculatePlanReliability(ExecutionPlan plan)
        {
            double totalReliability = 1.0;

            foreach (var step in plan.Steps)
            {
                var node = _graph.GetNode(step.CapabilityId);
                if (node?.Metadata.TryGetValue("reliabilityScore", out var reliability) == true)
                {
                    totalReliability *= Convert.ToDouble(reliability);
                }
            }

            return totalReliability;
        }

        /// <summary>
        /// Selects plan with best balance of cost, speed, and reliability.
        /// Uses weighted scoring: 40% speed, 30% cost, 30% reliability.
        /// </summary>
        private ExecutionPlan SelectBalancedPlan(List<ExecutionPlan> plans)
        {
            // Normalize metrics to 0-1 scale
            var maxDuration = plans.Max(p => p.EstimatedTotalDurationMs);
            var maxCost = plans.Max(p => p.EstimatedTotalCostUsd);

            return plans
                .Select(plan => new
                {
                    Plan = plan,
                    Score = CalculateBalancedScore(plan, maxDuration, maxCost)
                })
                .OrderByDescending(x => x.Score)
                .First()
                .Plan;
        }

        /// <summary>
        /// Calculates balanced score (higher is better).
        /// </summary>
        private double CalculateBalancedScore(ExecutionPlan plan, double maxDuration, decimal maxCost)
        {
            // Normalize metrics (invert so higher is better)
            var speedScore = maxDuration > 0 ? 1.0 - (plan.EstimatedTotalDurationMs / maxDuration) : 1.0;
            var costScore = maxCost > 0 ? 1.0 - (double)(plan.EstimatedTotalCostUsd / maxCost) : 1.0;
            var reliabilityScore = CalculatePlanReliability(plan);

            // Weighted combination: 40% speed, 30% cost, 30% reliability
            return (0.4 * speedScore) + (0.3 * costScore) + (0.3 * reliabilityScore);
        }

        /// <summary>
        /// Finds all alternative capabilities using "alternative_to" relationships.
        /// </summary>
        private List<string> FindAlternatives(string capabilityId)
        {
            var alternatives = new List<string> { capabilityId };

            // Find capabilities that are alternatives to this one
            var incomingAlternatives = _graph.GetIncomingEdges(capabilityId, "alternative_to");
            alternatives.AddRange(incomingAlternatives.Select(e => e.SourceId));

            // Find capabilities that this one is alternative to
            var outgoingAlternatives = _graph.GetOutgoingEdges(capabilityId, "alternative_to");
            alternatives.AddRange(outgoingAlternatives.Select(e => e.TargetId));

            return alternatives.Distinct().ToList();
        }

        /// <summary>
        /// Gets the cost of a capability from its metadata.
        /// </summary>
        private decimal GetCapabilityCost(string capabilityId)
        {
            var node = _graph.GetNode(capabilityId);
            if (node?.Metadata.TryGetValue("costPerOperationUsd", out var cost) == true)
            {
                return Convert.ToDecimal(cost);
            }
            return 0;
        }

        /// <summary>
        /// Gets the duration of a capability from its metadata.
        /// </summary>
        private double GetCapabilityDuration(string capabilityId)
        {
            var node = _graph.GetNode(capabilityId);
            if (node?.Metadata.TryGetValue("avgLatencyMs", out var duration) == true)
            {
                return Convert.ToDouble(duration);
            }
            return 0;
        }
    }

    /// <summary>
    /// Optimization objectives for execution plans.
    /// </summary>
    public enum OptimizationObjective
    {
        /// <summary>Minimize monetary cost (choose cheapest).</summary>
        MinimizeCost,

        /// <summary>Minimize execution duration (choose fastest).</summary>
        MinimizeDuration,

        /// <summary>Maximize reliability (choose most stable).</summary>
        MaximizeReliability,

        /// <summary>Balance cost, speed, and reliability.</summary>
        BalancedEfficiency
    }

    /// <summary>
    /// Result of cost savings calculation.
    /// </summary>
    public class CostSavingsResult
    {
        /// <summary>Cost savings in USD.</summary>
        public decimal CostSavingsUsd { get; set; }

        /// <summary>Cost savings as percentage.</summary>
        public double CostSavingsPercent { get; set; }

        /// <summary>Duration savings in milliseconds.</summary>
        public double DurationSavingsMs { get; set; }

        /// <summary>Duration savings as percentage.</summary>
        public double DurationSavingsPercent { get; set; }
    }
}
