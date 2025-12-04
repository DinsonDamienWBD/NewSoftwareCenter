using System;
using System.Collections.Generic;

namespace SoftwareCenter.Core.UI
{
    /// <summary>
    /// A generic container for a UI region.
    /// Represents a "Packet" of UI sent from a Module to the Host.
    /// </summary>
    public class ContentPart
    {
        // --- IDENTITY & TRACING ---

        /// <summary>
        /// Who owns this UI? (e.g., "GitModule").
        /// Essential for error logging and unloading.
        /// </summary>
        public string SourceId { get; set; } = string.Empty;

        /// <summary>
        /// The Trace ID of the operation that created this UI.
        /// (e.g., If this is the "Search Results" view, this ID links back to the "Search" command).
        /// </summary>
        public Guid? TraceId { get; set; }

        // --- DESTINATION (Receiver) ---

        /// <summary>
        /// The physical zone to render into.
        /// </summary>
        public UIZone TargetZone { get; set; }

        /// <summary>
        /// The logical slot for overrides (e.g., "Settings.SourceManager").
        /// </summary>
        public string? RegionName { get; set; }

        /// <summary>
        /// Higher numbers override lower numbers (Collision Resolution).
        /// </summary>
        public int Priority { get; set; } = 0;

        // --- PAYLOAD ---

        public Guid ViewId { get; set; } = Guid.NewGuid();
        public string? MetaTitle { get; set; }

        /// <summary>
        /// The schema definition. Host renders these.
        /// </summary>
        public List<UIControl> Controls { get; set; } = new List<UIControl>();
    }
}