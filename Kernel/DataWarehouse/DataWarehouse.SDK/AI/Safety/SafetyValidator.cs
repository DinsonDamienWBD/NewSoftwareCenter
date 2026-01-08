namespace DataWarehouse.SDK.AI.Safety
{
    /// <summary>
    /// Validates safety of AI operations before execution.
    /// Performs pre-execution checks to prevent dangerous operations.
    ///
    /// Used by AI Runtime to:
    /// - Validate input parameters
    /// - Check resource limits
    /// - Detect malicious patterns
    /// - Prevent data loss
    /// - Enforce security policies
    ///
    /// Validation types:
    /// - Input validation (SQL injection, path traversal)
    /// - Resource validation (memory, disk, cost limits)
    /// - Permission validation (user can execute this?)
    /// - Conflict validation (operation safe with current state?)
    /// - Pattern validation (detect suspicious behavior)
    /// </summary>
    public class SafetyValidator
    {
        private readonly List<SafetyRule> _rules = [];
        private readonly Dictionary<string, int> _failureCount = [];

        public SafetyValidator()
        {
            // Register default safety rules
            RegisterDefaultRules();
        }

        /// <summary>
        /// Validates an operation before execution.
        /// Returns validation result with errors if unsafe.
        /// </summary>
        /// <param name="operation">Operation to validate.</param>
        /// <returns>Validation result.</returns>
        public SafetyValidationResult Validate(OperationRequest operation)
        {
            ArgumentNullException.ThrowIfNull(operation);

            var result = new SafetyValidationResult { IsSafe = true };

            // Run all safety rules
            foreach (var rule in _rules)
            {
                var ruleResult = rule.Validate(operation);
                if (!ruleResult.Passed)
                {
                    result.IsSafe = false;
                    result.Violations.Add(new SafetyViolation
                    {
                        RuleName = rule.Name,
                        Severity = rule.Severity,
                        Message = ruleResult.Message,
                        RecommendedAction = ruleResult.RecommendedAction
                    });

                    // Track failures for rate limiting
                    TrackFailure(operation.CapabilityId ?? "unknown");
                }
            }

            return result;
        }

        /// <summary>
        /// Registers a custom safety rule.
        /// </summary>
        /// <param name="rule">Rule to register.</param>
        public void RegisterRule(SafetyRule rule)
        {
            ArgumentNullException.ThrowIfNull(rule);

            _rules.Add(rule);
        }

        /// <summary>
        /// Registers default safety rules.
        /// </summary>
        private void RegisterDefaultRules()
        {
            // Rule 1: No SQL injection patterns
            RegisterRule(new SafetyRule
            {
                Name = "NoSQLInjection",
                Severity = SafetySeverity.Critical,
                Validate = op =>
                {
                    foreach (var param in op.Parameters.Values)
                    {
                        if (param is string str && ContainsSQLInjection(str))
                        {
                            return new RuleValidationResult
                            {
                                Passed = false,
                                Message = "Potential SQL injection detected in parameters",
                                RecommendedAction = "Sanitize input or use parameterized queries"
                            };
                        }
                    }
                    return RuleValidationResult.Pass();
                }
            });

            // Rule 2: No path traversal
            RegisterRule(new SafetyRule
            {
                Name = "NoPathTraversal",
                Severity = SafetySeverity.High,
                Validate = op =>
                {
                    foreach (var param in op.Parameters.Values)
                    {
                        if (param is string str && ContainsPathTraversal(str))
                        {
                            return new RuleValidationResult
                            {
                                Passed = false,
                                Message = "Path traversal attempt detected (../ or ..\\ in path)",
                                RecommendedAction = "Use absolute paths or sanitize input"
                            };
                        }
                    }
                    return RuleValidationResult.Pass();
                }
            });

            // Rule 3: Cost limits
            RegisterRule(new SafetyRule
            {
                Name = "CostLimit",
                Severity = SafetySeverity.Medium,
                Validate = op =>
                {
                    if (op.EstimatedCostUsd.HasValue && op.EstimatedCostUsd.Value > 10.00m)
                    {
                        return new RuleValidationResult
                        {
                            Passed = false,
                            Message = $"Operation cost (${op.EstimatedCostUsd.Value:F2}) exceeds limit ($10.00)",
                            RecommendedAction = "Split operation or request budget increase"
                        };
                    }
                    return RuleValidationResult.Pass();
                }
            });

            // Rule 4: Data size limits
            RegisterRule(new SafetyRule
            {
                Name = "DataSizeLimit",
                Severity = SafetySeverity.Medium,
                Validate = op =>
                {
                    if (op.DataSizeBytes.HasValue && op.DataSizeBytes.Value > 10L * 1024 * 1024 * 1024) // 10GB
                    {
                        return new RuleValidationResult
                        {
                            Passed = false,
                            Message = $"Data size ({op.DataSizeBytes.Value / (1024 * 1024 * 1024)}GB) exceeds limit (10GB)",
                            RecommendedAction = "Split data into chunks or use streaming"
                        };
                    }
                    return RuleValidationResult.Pass();
                }
            });

            // Rule 5: Rate limiting (prevent abuse)
            RegisterRule(new SafetyRule
            {
                Name = "RateLimit",
                Severity = SafetySeverity.High,
                Validate = op =>
                {
                    var capId = op.CapabilityId ?? "unknown";
                    if (_failureCount.TryGetValue(capId, out var count) && count > 10)
                    {
                        return new RuleValidationResult
                        {
                            Passed = false,
                            Message = $"Too many failures for capability '{capId}' (rate limited)",
                            RecommendedAction = "Check capability configuration or wait before retrying"
                        };
                    }
                    return RuleValidationResult.Pass();
                }
            });
        }

        /// <summary>
        /// Checks for SQL injection patterns.
        /// </summary>
        private static bool ContainsSQLInjection(string input)
        {
            var patterns = new[]
            {
                "' OR '1'='1",
                "'; DROP TABLE",
                "' UNION SELECT",
                "' OR 1=1",
                "'; --",
                "' OR 'a'='a"
            };

            return patterns.Any(p => input.Contains(p, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Checks for path traversal patterns.
        /// </summary>
        private static bool ContainsPathTraversal(string input)
        {
            return input.Contains("../") || input.Contains("..\\");
        }

        /// <summary>
        /// Tracks failures for rate limiting.
        /// </summary>
        private void TrackFailure(string capabilityId)
        {
            if (!_failureCount.TryGetValue(capabilityId, out int value))
            {
                value = 0;
                _failureCount[capabilityId] = value;
            }

            _failureCount[capabilityId] = ++value;
        }

        /// <summary>
        /// Resets failure count for a capability.
        /// </summary>
        public void ResetFailureCount(string capabilityId)
        {
            _failureCount.Remove(capabilityId);
        }
    }

    /// <summary>
    /// Represents an operation to validate.
    /// </summary>
    public class OperationRequest
    {
        public string? CapabilityId { get; set; }
        public Dictionary<string, object> Parameters { get; init; } = [];
        public decimal? EstimatedCostUsd { get; set; }
        public long? DataSizeBytes { get; set; }
        public string? UserId { get; set; }
        public Dictionary<string, object> Metadata { get; init; } = [];
    }

    /// <summary>
    /// Result of safety validation.
    /// </summary>
    public class SafetyValidationResult
    {
        public bool IsSafe { get; set; }
        public List<SafetyViolation> Violations { get; init; } = [];

        public string GetErrorMessage()
        {
            if (IsSafe)
                return string.Empty;

            return string.Join("; ", Violations.Select(v => v.Message));
        }
    }

    /// <summary>
    /// A safety rule violation.
    /// </summary>
    public class SafetyViolation
    {
        public string RuleName { get; set; } = string.Empty;
        public SafetySeverity Severity { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? RecommendedAction { get; set; }
    }

    /// <summary>
    /// Safety rule definition.
    /// </summary>
    public class SafetyRule
    {
        public string Name { get; set; } = string.Empty;
        public SafetySeverity Severity { get; set; } = SafetySeverity.Medium;
        public Func<OperationRequest, RuleValidationResult> Validate { get; set; } = _ => RuleValidationResult.Pass();
    }

    /// <summary>
    /// Result of a single rule validation.
    /// </summary>
    public class RuleValidationResult
    {
        public bool Passed { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? RecommendedAction { get; set; }

        public static RuleValidationResult Pass() => new() { Passed = true };
    }

    /// <summary>
    /// Severity levels for safety violations.
    /// </summary>
    public enum SafetySeverity
    {
        Low,
        Medium,
        High,
        Critical
    }
}
