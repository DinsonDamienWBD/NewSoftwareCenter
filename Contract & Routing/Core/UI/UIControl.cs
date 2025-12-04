using System.Collections.Generic;

namespace SoftwareCenter.Core.UI
{
    /// <summary>
    /// Defines a single UI element abstractly.
    /// The Host converts these into native widgets (WPF Controls, Blazor Components, etc.).
    /// This keeps the Core and Modules purely logical and UI-framework agnostic.
    /// </summary>
    public class UIControl
    {
        /// <summary>
        /// The type of widget to render.
        /// Examples: "Button", "Label", "TextField", "StackPanel", "Icon".
        /// </summary>
        public string ControlType { get; set; } = "Label";

        /// <summary>
        /// The display text or content.
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// Reference to an icon resource (e.g., "Save", "Settings", "Alert").
        /// </summary>
        public string? Icon { get; set; }

        /// <summary>
        /// Determines if the control is rendered.
        /// </summary>
        public bool IsVisible { get; set; } = true;

        /// <summary>
        /// Determines if the user can interact with the control.
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// The key used to bind this control's value to the generic Data Dictionary.
        /// (Rule 24: Reactive UI).
        /// Example: "System.CpuUsage" -> Updates text automatically when that key changes.
        /// </summary>
        public string? BindKey { get; set; }

        /// <summary>
        /// Defines interactivity.
        /// Key: The Event Name (e.g., "Click", "Hover", "Change").
        /// Value: The Command Name to trigger (e.g., "Module.Save", "Nav.Home").
        /// </summary>
        public Dictionary<string, string> Actions { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Container for nested controls (used when ControlType is a layout container like "StackPanel").
        /// </summary>
        public List<UIControl> Children { get; set; } = new List<UIControl>();

        /// <summary>
        /// Optional hints for the renderer.
        /// Examples: { "Color", "Red" }, { "FontSize", 14 }, { "Width", "100%" }.
        /// </summary>
        public Dictionary<string, object> Styles { get; set; } = new Dictionary<string, object>();
    }
}