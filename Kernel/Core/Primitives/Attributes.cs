namespace Core.Primitives
{
    // --- SOURCE GEN ---

    /// <summary>
    /// SOURCE GENERATOR HOOK.
    /// Marks a class as a Service Handler that should be registered in the Kernel.
    /// </summary>
    /// <remarks>
    /// Registers a class as a Service Handler.
    /// </remarks>
    /// <param name="priority"></param>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class RegisterServiceAttribute(Priority priority = Priority.Normal) : Attribute
    {
        /// <summary>
        /// Priority of the service registration.
        /// </summary>
        public Priority Priority { get; } = priority;
    }

    /// <summary>
    /// SOURCE GENERATOR HOOK.
    /// Marks a class (ViewModel) as a UI fragment to be composed into the Shell.
    /// </summary>
    /// <remarks>
    /// Constructor to register a UI fragment.
    /// </remarks>
    /// <param name="zoneId"></param>
    /// <param name="templateId"></param>
    /// <param name="order"></param>
    /// <param name="icon"></param>
    /// <param name="label"></param>
    /// <param name="permission"></param>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class RegisterUiAttribute(
        string zoneId,
        string templateId,
        int order = 0,
        string icon = "",
        string label = "",
        string permission = "") : Attribute
    {
        /// <summary>
        /// Zone ID where the UI fragment should be placed.
        /// </summary>
        public string ZoneId { get; } = zoneId;

        /// <summary>
        /// Template ID of the UI fragment.
        /// </summary>
        public string TemplateId { get; } = templateId;

        /// <summary>
        /// Order of the UI fragment within the zone.
        /// </summary>
        public int Order { get; } = order;

        /// <summary>
        /// Icon representing the UI fragment.
        /// </summary>
        public string Icon { get; } = icon;

        /// <summary>
        /// Label for the UI fragment.
        /// </summary>
        public string Label { get; } = label;

        /// <summary>
        /// Permission required to access the UI fragment.
        /// </summary>
        public string RequiredPermission { get; } = permission;
    }

    // --- METADATA ---

    /// <summary>
    /// Declares a hard dependency on another module ID. 
    /// The Kernel ensures the target module is loaded before this one.
    /// </summary>
    /// <remarks>
    /// Adds a hard dependency on another module.
    /// </remarks>
    /// <param name="moduleId"></param>
    /// <param name="minVersion"></param>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class DependsOnAttribute(string moduleId, string minVersion = "0.0.0") : Attribute
    {
        /// <summary>
        /// ID of the module this module depends on.
        /// </summary>
        public string ModuleId { get; } = moduleId;

        /// <summary>
        /// Minimum version to depend on
        /// </summary>
        public string MinVersion { get; } = minVersion;
    }

    /// <summary>
    /// Provides human-readable documentation for the Runtime Help System.
    /// Used when XML documentation is unavailable.
    /// </summary>
    /// <remarks>
    /// Adds a description to a class or property
    /// </remarks>
    /// <param name="text"></param>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
    public class DescriptionAttribute(string text) : Attribute
    {
        /// <summary>
        /// Description text.
        /// </summary>
        public string Text { get; } = text;
    }

    /// <summary>
    /// Instructs the serializer to ignore this property.
    /// Decouples Core from Json.NET / XML attributes.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class DoNotSerializeAttribute : Attribute { }

    // --- Protection ---

    /// <summary>
    /// Apply Rate limit
    /// </summary>
    /// <param name="limit"></param>
    /// <param name="periodSeconds"></param>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class RateLimitAttribute(int limit, int periodSeconds) : Attribute
    {
        /// <summary>
        /// Limit amount
        /// </summary>
        public int Limit { get; } = limit;

        /// <summary>
        /// Period in seconds
        /// </summary>
        public int PeriodSeconds { get; } = periodSeconds;
    }

    // --- GDPR Compliance ---

    /// <summary>
    /// Comply with GDPR
    /// </summary>
    /// <param name="days"></param>
    [AttributeUsage(AttributeTargets.Class)]
    public class RetentionAttribute(int days) : Attribute
    {
        /// <summary>
        /// Number of days
        /// </summary>
        public int Days { get; } = days;
    }

    // --- Infrastructure Requirements ---

    /// <summary>
    /// Attribute for storage capabilities
    /// </summary>
    /// <param name="capability"></param>
    [AttributeUsage(AttributeTargets.Class)]
    public class RequiresCapabilityAttribute(StorageCapabilities capability) : Attribute
    {
        /// <summary>
        /// Storage capabilities
        /// </summary>
        public StorageCapabilities Capability { get; } = capability;
    }

    // --- SECURITY & OPS ---

    /// <summary>
    /// Marks a property as sensitive (PII/Secret). 
    /// The Flight Recorder will automatically redact this value.
    /// </summary>
    /// <param name="classification"></param>
    [AttributeUsage(AttributeTargets.Property)]
    public class SensitiveAttribute(DataClassification classification = DataClassification.Confidential) : Attribute
    {
        /// <summary>
        /// Sensitivity classification
        /// </summary>
        public DataClassification Classification { get; } = classification;
    }

    /// <summary>
    /// Declares a feature gate
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    /// <param name="featureId"></param>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class FeatureGateAttribute(string featureId) : Attribute
    {
        /// <summary>
        /// ID of a feature
        /// </summary>
        public string FeatureId { get; } = featureId;
    }

    /// <summary>
    /// Enumeration for service registration priority levels.
    /// </summary>
    public enum RetryStrategy 
    {
        /// <summary>
        /// Linear retry strategy.
        /// </summary>
        Linear,

        /// <summary>
        /// Exponential retry strategy.
        /// </summary>
        Exponential,

        /// <summary>
        /// Constant retry strategy.
        /// </summary>
        Constant
    }

    /// <summary>
    /// Retries the decorated class's operations based on the specified policy.
    /// </summary>
    /// <remarks>
    /// Constructor to define a retry policy.
    /// </remarks>
    /// <param name="maxRetries"></param>
    /// <param name="delayMs"></param>
    /// <param name="strategy"></param>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class RetryPolicyAttribute(int maxRetries = 3, int delayMs = 500, RetryStrategy strategy = RetryStrategy.Linear) : Attribute
    {
        /// <summary>
        /// Maximum number of retry attempts.
        /// </summary>
        public int MaxRetries { get; } = maxRetries;

        /// <summary>
        /// Delay between retries in milliseconds.
        /// </summary>
        public int DelayMs { get; } = delayMs;

        /// <summary>
        /// Strategy used for retries.
        /// </summary>
        public RetryStrategy Strategy { get; } = strategy;
    }
}