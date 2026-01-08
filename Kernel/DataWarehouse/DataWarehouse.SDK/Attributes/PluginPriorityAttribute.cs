using DataWarehouse.SDK.Contracts;

namespace DataWarehouse.SDK.Attributes
{
    /// <summary>
    /// Declares the load priority of a plugin. 
    /// Higher values are loaded first. Used for conflict resolution.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the PluginPriorityAttribute.
    /// </remarks>
    /// <param name="priority">Higher numbers indicate preference.</param>
    /// <param name="optimizedFor">The mode this plugin performs best in.</param>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class PluginPriorityAttribute(int priority, OperatingMode optimizedFor) : Attribute
    {
        /// <summary>
        /// The numeric priority. Higher is better.
        /// </summary>
        public int Priority { get; } = priority;

        /// <summary>
        /// The intended operating mode for this plugin.
        /// </summary>
        public OperatingMode OptimizedFor { get; } = optimizedFor;
    }
}