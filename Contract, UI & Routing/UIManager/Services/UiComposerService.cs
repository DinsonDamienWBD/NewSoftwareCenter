using HtmlAgilityPack;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using System.Threading.Tasks;
using System.Linq; // Added for SelectNodes

namespace SoftwareCenter.UIManager.Services
{
    public class UiComposerService
    {
        private readonly IWebHostEnvironment _env;
        private readonly UiTemplateService _templateService;

        public UiComposerService(IWebHostEnvironment env, UiTemplateService templateService)
        {
            _env = env;
            _templateService = templateService;
        }

        public async Task<string> GetComposedHtmlAsync()
        {
            // 1. Get the base index.html content
            var indexPath = Path.Combine(_env.WebRootPath, "Html", "index.html");
            if (!File.Exists(indexPath))
            {
                return "<h1>Error: Base index.html not found!</h1>";
            }
            var html = await File.ReadAllTextAsync(indexPath);

            // 2. Load the HTML into HtmlAgilityPack
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // 3. Inject zone HTMLs
            await InjectZoneHtmlAsync(doc, "titlebar-zone");
            await InjectZoneHtmlAsync(doc, "nav-rail-zone");
            await InjectZoneHtmlAsync(doc, "content-zone");

            // 4. Return the composed HTML
            return doc.DocumentNode.OuterHtml;
        }

        private async Task InjectZoneHtmlAsync(HtmlDocument doc, string zoneId)
        {
            // Find the placeholder div in the main index.html
            var placeholderElement = doc.DocumentNode.SelectSingleNode($"//div[@id='{zoneId}']");
            if (placeholderElement != null)
            {
                // Get the HTML content for the zone from UiTemplateService
                var fullZoneHtml = await _templateService.GetZoneHtmlAsync(zoneId);
                if (!string.IsNullOrEmpty(fullZoneHtml))
                {
                    // Strip the leading "-->" comment if present (from the zone HTML files)
                    fullZoneHtml = fullZoneHtml.Replace("-->", "").Trim();

                    var fragmentDoc = new HtmlDocument();
                    fragmentDoc.LoadHtml(fullZoneHtml);
                    var fragmentRoot = fragmentDoc.DocumentNode.SelectSingleNode("//div"); // Assuming the zone HTML is always a single div

                    if (fragmentRoot != null)
                    {
                        // Replace the inner HTML of the placeholder div with the inner HTML of the zone fragment
                        placeholderElement.InnerHtml = fragmentRoot.InnerHtml;

                        // Transfer attributes from the fragment root to the placeholderElement
                        // This ensures data-GUID and other attributes on the top-level zone div are preserved/set
                        foreach (var attr in fragmentRoot.Attributes)
                        {
                            // Avoid overwriting existing attributes like 'id' if they are meant to be unique to the index.html placeholder
                            if (attr.Name != "id")
                            {
                                placeholderElement.SetAttributeValue(attr.Name, attr.Value);
                            }
                        }
                    }
                }
            }
        }
    }
}
