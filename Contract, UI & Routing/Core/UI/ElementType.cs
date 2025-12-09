namespace SoftwareCenter.Core.UI
{
    /// <summary>
    /// Defines the types of UI elements that can be created and managed.
    /// </summary>
    public enum ElementType
    {
        /// <summary>
        /// A container that can hold other elements.
        /// </summary>
        Panel,

        /// <summary>
        /// An interactive button.
        /// </summary>
        Button,

        /// <summary>
        /// A static text label.
        /// </summary>
        Label,

        /// <summary>
        /// A text input box.
        /// </summary>
        TextInput,

        /// <summary>
        /// A generic container for displaying a card-like element.
        /// </summary>
        Card,

        /// <summary>
        /// A generic UI fragment provided with direct HTML, CSS, and JS content.
        /// </summary>
        Fragment
    }
}
