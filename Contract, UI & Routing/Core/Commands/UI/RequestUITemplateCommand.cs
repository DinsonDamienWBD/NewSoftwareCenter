namespace SoftwareCenter.Core.Commands.UI
{
    public class RequestUITemplateCommand : ICommand<string>
    {
        public string TemplateType { get; }
        public string ParentId { get; }
        public Dictionary<string, object> InitialProperties { get; }

        public RequestUITemplateCommand(string templateType, string parentId, Dictionary<string, object> initialProperties = null)
        {
            TemplateType = templateType;
            ParentId = parentId;
            InitialProperties = initialProperties ?? new Dictionary<string, object>();
        }
    }
}
