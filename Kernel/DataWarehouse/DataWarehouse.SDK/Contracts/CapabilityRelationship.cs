namespace DataWarehouse.SDK.Contracts
{
    /// <summary>
    /// Defines a relationship between capabilities.
    /// Used by AI for execution planning, capability chaining, and conflict detection.
    ///
    /// Relationships help AI understand:
    /// - Which capabilities can work together
    /// - Which capabilities should run in sequence
    /// - Which capabilities are alternatives
    /// - Which capabilities conflict
    /// </summary>
    public class CapabilityRelationship
    {
        /// <summary>
        /// Type of relationship.
        ///
        /// Common types:
        /// - "flows_into": Output of source capability feeds into target capability
        /// - "depends_on": Source capability requires target capability to function
        /// - "alternative_to": Source capability can substitute for target capability
        /// - "compatible_with": Source capability can run alongside target capability
        /// - "incompatible_with": Source capability conflicts with target capability
        /// - "precedes": Source should run before target in execution plan
        /// - "follows": Source should run after target in execution plan
        /// - "replaces": Source is newer/better version of target
        /// </summary>
        public string RelationType { get; init; } = string.Empty;

        /// <summary>
        /// Target capability ID this relationship points to.
        ///
        /// Examples:
        /// - "storage.local.save"
        /// - "transform.aes.apply"
        /// - "metadata.sqlite.index"
        /// </summary>
        public string TargetCapabilityId { get; init; } = string.Empty;

        /// <summary>
        /// Human-readable description of the relationship.
        /// Used by AI to understand why this relationship exists.
        ///
        /// Examples:
        /// - "Compressed data should be encrypted for security"
        /// - "Encryption requires key from key management"
        /// - "GZip and Zstandard are alternative compression algorithms"
        /// </summary>
        public string Description { get; init; } = string.Empty;

        /// <summary>
        /// Strength of relationship (0.0 to 1.0).
        /// Higher values indicate stronger relationships.
        /// Used by AI for prioritization.
        ///
        /// Examples:
        /// - 1.0: Mandatory relationship (hard dependency)
        /// - 0.8: Strong recommendation
        /// - 0.5: Optional but beneficial
        /// - 0.2: Weak relationship
        /// </summary>
        public double Strength { get; init; } = 1.0;

        /// <summary>
        /// Whether this relationship is bidirectional.
        /// If true, the reverse relationship also holds.
        ///
        /// Example:
        /// - "compatible_with" is often bidirectional
        /// - "depends_on" is usually unidirectional
        /// - "flows_into" is unidirectional
        /// </summary>
        public bool IsBidirectional { get; init; } = false;

        /// <summary>
        /// Constructs a capability relationship.
        /// </summary>
        public CapabilityRelationship()
        {
        }

        /// <summary>
        /// Constructs a capability relationship with specified type and target.
        /// </summary>
        /// <param name="relationType">Type of relationship.</param>
        /// <param name="targetCapabilityId">Target capability ID.</param>
        public CapabilityRelationship(string relationType, string targetCapabilityId)
        {
            RelationType = relationType;
            TargetCapabilityId = targetCapabilityId;
        }

        /// <summary>
        /// Constructs a fully specified capability relationship.
        /// </summary>
        /// <param name="relationType">Type of relationship.</param>
        /// <param name="targetCapabilityId">Target capability ID.</param>
        /// <param name="description">Human-readable description.</param>
        /// <param name="strength">Relationship strength (0.0 to 1.0).</param>
        public CapabilityRelationship(
            string relationType,
            string targetCapabilityId,
            string description,
            double strength = 1.0)
        {
            RelationType = relationType;
            TargetCapabilityId = targetCapabilityId;
            Description = description;
            Strength = strength;
        }
    }
}
