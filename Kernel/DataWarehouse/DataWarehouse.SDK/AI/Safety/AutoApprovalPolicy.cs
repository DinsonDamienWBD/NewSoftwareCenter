using System;
using System.Collections.Generic;
using System.Linq;

namespace DataWarehouse.SDK.AI.Safety
{
    /// <summary>
    /// Defines policies for automatic approval of AI actions.
    /// Determines which operations are safe enough to execute without human approval.
    ///
    /// Used by ApprovalQueue to:
    /// - Auto-approve low-risk operations
    /// - Require approval for high-risk operations
    /// - Enforce cost limits
    /// - Whitelist safe capabilities
    /// - Blacklist dangerous capabilities
    ///
    /// Policy types:
    /// - Cost-based: Auto-approve if cost below threshold
    /// - Capability-based: Whitelist/blacklist specific capabilities
    /// - Risk-based: Approve only low-risk operations
    /// - Category-based: Auto-approve entire categories (read-only, etc.)
    /// </summary>
    public class AutoApprovalPolicy
    {
        /// <summary>
        /// Maximum cost that can be auto-approved (USD).
        /// Operations exceeding this require human approval.
        /// Default: $0.10
        /// </summary>
        public decimal MaxAutoApprovalCostUsd { get; set; } = 0.10m;

        /// <summary>
        /// Capabilities that are always auto-approved (whitelist).
        /// Example: ["transform.gzip.apply", "storage.local.read"]
        /// </summary>
        public HashSet<string> WhitelistedCapabilities { get; init; } = new();

        /// <summary>
        /// Capabilities that always require approval (blacklist).
        /// Example: ["storage.*.delete", "security.*", "admin.*"]
        /// </summary>
        public HashSet<string> BlacklistedCapabilities { get; init; } = new();

        /// <summary>
        /// Risk levels that can be auto-approved.
        /// Example: ["low", "medium"]
        /// </summary>
        public HashSet<string> AutoApprovalRiskLevels { get; init; } = new() { "low" };

        /// <summary>
        /// Whether to auto-approve all read-only operations.
        /// Default: true (reads are generally safe)
        /// </summary>
        public bool AutoApproveReadOnly { get; set; } = true;

        /// <summary>
        /// Capability categories that are always safe.
        /// Example: ["metadata.search", "transform.compress"]
        /// </summary>
        public HashSet<string> SafeCategories { get; init; } = new()
        {
            "transform.compress",
            "transform.decompress",
            "metadata.search",
            "metadata.query"
        };

        /// <summary>
        /// Capability categories that always require approval.
        /// Example: ["storage.delete", "security", "admin"]
        /// </summary>
        public HashSet<string> DangerousCategories { get; init; } = new()
        {
            "storage.delete",
            "security.admin",
            "admin",
            "system"
        };

        /// <summary>
        /// Determines if a request should be auto-approved.
        /// </summary>
        /// <param name="request">Approval request to evaluate.</param>
        /// <returns>True if should auto-approve, false if needs human review.</returns>
        public bool ShouldAutoApprove(ApprovalRequest request)
        {
            if (request == null)
                return false;

            // Check blacklist first (highest priority)
            if (IsBlacklisted(request))
                return false;

            // Check whitelist (second priority)
            if (IsWhitelisted(request))
                return true;

            // Check cost threshold
            if (request.EstimatedCostUsd.HasValue && request.EstimatedCostUsd.Value > MaxAutoApprovalCostUsd)
                return false;

            // Check risk level
            if (!AutoApprovalRiskLevels.Contains(request.RiskLevel))
                return false;

            // Check dangerous categories
            if (request.CapabilityId != null && IsDangerousCategory(request.CapabilityId))
                return false;

            // Check safe categories
            if (request.CapabilityId != null && IsSafeCategory(request.CapabilityId))
                return true;

            // Check read-only
            if (AutoApproveReadOnly && IsReadOnly(request))
                return true;

            // Default: require approval for unknown operations
            return false;
        }

        /// <summary>
        /// Checks if capability is whitelisted.
        /// </summary>
        private bool IsWhitelisted(ApprovalRequest request)
        {
            if (request.CapabilityId == null)
                return false;

            return WhitelistedCapabilities.Contains(request.CapabilityId);
        }

        /// <summary>
        /// Checks if capability is blacklisted.
        /// Supports wildcards (e.g., "storage.*.delete").
        /// </summary>
        private bool IsBlacklisted(ApprovalRequest request)
        {
            if (request.CapabilityId == null)
                return false;

            foreach (var pattern in BlacklistedCapabilities)
            {
                if (MatchesPattern(request.CapabilityId, pattern))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if capability belongs to a safe category.
        /// </summary>
        private bool IsSafeCategory(string capabilityId)
        {
            foreach (var category in SafeCategories)
            {
                if (capabilityId.StartsWith(category))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if capability belongs to a dangerous category.
        /// </summary>
        private bool IsDangerousCategory(string capabilityId)
        {
            foreach (var category in DangerousCategories)
            {
                if (capabilityId.Contains(category))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if operation is read-only.
        /// Read operations: search, query, read, load, get, list
        /// </summary>
        private bool IsReadOnly(ApprovalRequest request)
        {
            if (request.CapabilityId == null)
                return false;

            var readOnlyKeywords = new[] { "read", "load", "get", "search", "query", "list", "find" };

            return readOnlyKeywords.Any(keyword =>
                request.CapabilityId.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Matches capability ID against pattern with wildcards.
        /// Example: "storage.*.delete" matches "storage.s3.delete"
        /// </summary>
        private bool MatchesPattern(string capabilityId, string pattern)
        {
            if (pattern == "*")
                return true;

            // Simple wildcard matching
            var parts = pattern.Split('*');
            int position = 0;

            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part))
                    continue;

                int index = capabilityId.IndexOf(part, position, StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                    return false;

                position = index + part.Length;
            }

            return true;
        }

        /// <summary>
        /// Creates a permissive policy (auto-approves most operations).
        /// Use for development or trusted environments.
        /// </summary>
        public static AutoApprovalPolicy Permissive()
        {
            return new AutoApprovalPolicy
            {
                MaxAutoApprovalCostUsd = 1.00m,
                AutoApprovalRiskLevels = new HashSet<string> { "low", "medium" },
                AutoApproveReadOnly = true,
                BlacklistedCapabilities = new HashSet<string>
                {
                    "storage.*.delete",
                    "admin.*",
                    "system.*"
                }
            };
        }

        /// <summary>
        /// Creates a strict policy (requires approval for most operations).
        /// Use for production or untrusted environments.
        /// </summary>
        public static AutoApprovalPolicy Strict()
        {
            return new AutoApprovalPolicy
            {
                MaxAutoApprovalCostUsd = 0.01m,
                AutoApprovalRiskLevels = new HashSet<string> { "low" },
                AutoApproveReadOnly = true,
                WhitelistedCapabilities = new HashSet<string>
                {
                    "metadata.search",
                    "metadata.query"
                },
                BlacklistedCapabilities = new HashSet<string>
                {
                    "storage.*.delete",
                    "storage.*.write",
                    "transform.encrypt",
                    "transform.decrypt",
                    "security.*",
                    "admin.*",
                    "system.*"
                }
            };
        }
    }
}
