using Microsoft.AspNetCore.Hosting;
using SoftwareCenter.Core.UI;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SoftwareCenter.Host.Services
{
    public class HostTemplateService : ITemplateService
    {
        private readonly IWebHostEnvironment _webHostEnvironment;

        public HostTemplateService(IWebHostEnvironment webHostEnvironment)
        {
            _webHostEnvironment = webHostEnvironment;
        }

        public async Task<string> GetTemplateHtml(string templateType, Dictionary<string, object> parameters)
        {
            var templatePath = Path.Combine(_webHostEnvironment.WebRootPath, "templates", $"{templateType.ToLowerInvariant()}.html");

            if (!File.Exists(templatePath))
            {
                // Fallback to a default template if specific one not found, or throw.
                // For now, let's just return an empty string or throw an exception.
                throw new FileNotFoundException($"Template file not found for type: {templateType} at {templatePath}");
            }

            var htmlContent = await File.ReadAllTextAsync(templatePath);

            // Process {{if}} statements first
            htmlContent = ProcessConditionalStatements(htmlContent, parameters);

            // Perform placeholder replacements
            foreach (var param in parameters)
            {
                htmlContent = htmlContent.Replace($"{{{{{param.Key}}}}}", param.Value?.ToString() ?? string.Empty);
            }

            return htmlContent;
        }

        private string ProcessConditionalStatements(string html, Dictionary<string, object> parameters)
        {
            // Regex to find {{if CONDITION}}...{{endif}} blocks
            var ifRegex = new Regex(@"\{\{if\s*(\w+)\}\}(.*?)\{\{endif\}\}", RegexOptions.Singleline | RegexOptions.IgnoreCase);

            return ifRegex.Replace(html, match =>
            {
                var conditionKey = match.Groups[1].Value.Trim();
                var innerContent = match.Groups[2].Value;

                if (parameters.TryGetValue(conditionKey, out object value) && value is bool condition && condition)
                {
                    return innerContent;
                }
                else if (parameters.TryGetValue(conditionKey, out value) && value != null && !(value is bool) && !string.IsNullOrEmpty(value.ToString()))
                {
                    // Treat non-boolean, non-null, non-empty string values as true
                    return innerContent;
                }
                return string.Empty; // Remove the block if condition is false or not met
            });
        }
    }
}
