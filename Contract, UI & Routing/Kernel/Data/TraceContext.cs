using System;
using System.Collections.Generic;
using System.Threading;

namespace SoftwareCenter.Kernel.Data
{
    /// <summary>
    /// Represents a single step in a command's execution pipeline.
    /// </summary>
    public class TraceHop
    {
        /// <summary>
        /// The name of the component that processed this step (e.g., "Kernel.Router", "Module.AppManager").
        /// </summary>
        public string Component { get; }

        /// <summary>
        /// A message describing the action taken at this hop.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// The timestamp when this hop was recorded.
        /// </summary>
        public DateTime Timestamp { get; } = DateTime.UtcNow;

        public TraceHop(string component, string message)
        {
            Component = component;
            Message = message;
        }
    }

    /// <summary>
    /// Represents a Trace Session.
    /// NOT static anymore. It is an object that holds the TraceId and History.
    /// It contains static members to manage the 'Ambient' (Thread-Local) context.
    /// </summary>
    public class TraceContext
    {
        // The Invisible Thread-Local Storage
        private static readonly AsyncLocal<TraceContext?> _current = new AsyncLocal<TraceContext?>();

        // Instance Properties
        public Guid TraceId { get; set; }
        public List<TraceHop> History { get; }

        public TraceContext()
        {
            TraceId = Guid.NewGuid();
            History = new List<TraceHop>();
        }

        public void AddHop(string entityId, string action)
        {
            History.Add(new TraceHop(entityId, action));
        }

        // --- STATIC ACCESSORS (The Ambient Context) ---

        /// <summary>
        /// Gets or sets the context for the current async flow.
        /// </summary>
        public static TraceContext? Current
        {
            get => _current.Value;
            set => _current.Value = value;
        }

        /// <summary>
        /// Helper: Returns the ID of the current context, or null if none exists.
        /// Setting this ensures a Context object exists.
        /// </summary>
        public static Guid? CurrentTraceId
        {
            get => Current?.TraceId;
            set
            {
                if (value == null) return;

                if (Current == null)
                {
                    Current = new TraceContext();
                }
                Current.TraceId = value.Value;
            }
        }

        /// <summary>
        /// Starts a fresh trace for the current thread.
        /// </summary>
        public static TraceContext StartNew()
        {
            var ctx = new TraceContext();
            Current = ctx;
            return ctx;
        }
    }
}