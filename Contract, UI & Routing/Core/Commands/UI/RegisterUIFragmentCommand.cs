using SoftwareCenter.Core.Routing;
using SoftwareCenter.Core.Data;

namespace SoftwareCenter.Core.Commands.UI
{
    public class RegisterUIFragmentCommand : ICommand<string>
    {
        public string? ParentId { get; }
        public string? SlotName { get; }
        public string HtmlContent { get; }
        public string? CssContent { get; }
        public string? JsContent { get; }
        public int Priority { get; }

        public RegisterUIFragmentCommand(string htmlContent, string? parentId = null, string? slotName = null, string? cssContent = null, string? jsContent = null, int priority = (int)HandlerPriority.Normal)
        {
            ParentId = parentId;
            SlotName = slotName;
            HtmlContent = htmlContent;
            CssContent = cssContent;
            JsContent = jsContent;
            Priority = priority;
        }
    }
}
