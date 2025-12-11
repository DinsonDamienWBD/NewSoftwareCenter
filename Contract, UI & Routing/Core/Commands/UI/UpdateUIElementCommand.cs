using System.Collections.Generic;
using System;

namespace SoftwareCenter.Core.Commands.UI
{
    public class UpdateUIElementCommand : ICommand
    {
        public string ElementId { get; }
        public Dictionary<string, object> UpdatedProperties { get; }

        [Obsolete("Use the constructor that takes a dictionary of properties.")]
        public UpdateUIElementCommand(string elementId, string? htmlContent = null, Dictionary<string, string>? attributesToSet = null, List<string>? attributesToRemove = null)
        {
            ElementId = elementId;
            UpdatedProperties = new Dictionary<string, object>();
            if(htmlContent != null)
                UpdatedProperties["HtmlContent"] = htmlContent;
            if(attributesToSet != null)
                UpdatedProperties["AttributesToSet"] = attributesToSet;
            if(attributesToRemove != null)
                UpdatedProperties["AttributesToRemove"] = attributesToRemove;
        }

        public UpdateUIElementCommand(string elementId, Dictionary<string, object> updatedProperties)
        {
            ElementId = elementId;
            UpdatedProperties = updatedProperties;
        }
    }
}