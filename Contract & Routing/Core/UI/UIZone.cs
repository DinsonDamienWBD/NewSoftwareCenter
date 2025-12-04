namespace SoftwareCenter.Core.UI
{
    /// <summary>
    /// Defines the 5 mandatory zones of the layout (Rule 23).
    /// The Host guarantees these zones exist; Modules target them by enum.
    /// </summary>
    public enum UIZone
    {
        /// <summary>
        /// The global window title area (e.g., "Software Center - Home").
        /// </summary>
        Title = 0,

        /// <summary>
        /// The notification area (Toasts, Alerts).
        /// </summary>
        Notification = 1,

        /// <summary>
        /// The Power/Session area (Exit, Minimize, Settings).
        /// </summary>
        Power = 2,

        /// <summary>
        /// The main navigation sidebar or ribbon.
        /// </summary>
        Navigation = 3,

        /// <summary>
        /// The main workspace where views are injected.
        /// </summary>
        Content = 4
    }
}