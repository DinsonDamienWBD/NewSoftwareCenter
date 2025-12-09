using SoftwareCenter.Core.UI;

namespace SoftwareCenter.Core.Events.UI
{
    /// <summary>
    /// Published when a new UI element is created and registered with the UIManager.
    /// The Host's notifier service will listen for this and send the data to the frontend.
    /// </summary>
    public class UIElementRegisteredEvent : IEvent
    {
        public UIElement NewElement { get; }

        /// <summary>
        /// The raw HTML content for the element. This might be from a template
        /// or directly provided by the requester.
        /// </summary>
        public string HtmlContent { get; }

        /// <summary>
        /// Optional CSS content to be injected for this element.
        /// </summary>
        public string? CssContent { get; }

        /// <summary>
        /// Optional JavaScript content to be injected for this element.
        /// </summary>
        public string? JsContent { get; }

        public UIElementRegisteredEvent(UIElement newElement, string htmlContent, string? cssContent = null, string? jsContent = null)
        {
            NewElement = newElement;
            HtmlContent = htmlContent;
            CssContent = cssContent;
            JsContent = jsContent;
        }
    }
}
