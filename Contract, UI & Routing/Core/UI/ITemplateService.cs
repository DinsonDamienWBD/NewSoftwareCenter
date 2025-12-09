using System.Collections.Generic;
using System.Threading.Tasks;

namespace SoftwareCenter.Core.UI
{
    /// <summary>
    /// Provides an abstraction for retrieving and rendering UI templates.
    /// Implementations will be responsible for locating and processing template files.
    /// </summary>
    public interface ITemplateService
    {
        /// <summary>
        /// Retrieves the HTML content for a specified template type,
        /// optionally performing placeholder replacements with provided parameters.
        /// </summary>
        /// <param name="templateType">The identifier for the template (e.g., "Card", "NavButton").</param>
        /// <param name="parameters">A dictionary of parameters to use for placeholder replacement in the template.</param>
        /// <returns>The processed HTML content of the template.</returns>
        Task<string> GetTemplateHtml(string templateType, Dictionary<string, object> parameters);
    }
}
