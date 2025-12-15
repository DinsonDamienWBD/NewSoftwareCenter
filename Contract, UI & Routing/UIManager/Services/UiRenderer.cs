using HtmlAgilityPack;
using Microsoft.AspNetCore.Hosting; // Added for IWebHostEnvironment
using SoftwareCenter.Core.UI;
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

namespace SoftwareCenter.UIManager.Services
{
    public class UiRenderer : Core.UI.IUiService
    {
        private readonly UiTemplateService _templates;
        private readonly IWebHostEnvironment _env; // Added IWebHostEnvironment

        public UiRenderer(UiTemplateService templates, IWebHostEnvironment env)
        {
            _templates = templates;
            _env = env; // Storing the injected environment
        }

        // --- 1. INITIAL LOAD LOGIC (Index + Zones) ---

        public async Task<string> GetComposedIndexPageAsync()
        {
            var debugMessages = new StringBuilder();
            debugMessages.AppendLine("<!-- DEBUG START -->");
            debugMessages.AppendLine("<!-- Entered GetComposedIndexPageAsync. -->");

            // 1. Load shell
            var indexPath = Path.Combine(_env.WebRootPath, "Html", "index.html");
            debugMessages.AppendLine($"<!-- Checking for index.html at: {indexPath} -->");
            if (!System.IO.File.Exists(indexPath))
            {
                debugMessages.AppendLine($"<!-- ERROR: index.html NOT FOUND at: {indexPath} -->");
                return $"<h1>Error: Base index.html not found!</h1>{debugMessages.ToString()}";
            }
            debugMessages.AppendLine($"<!-- index.html FOUND at: {indexPath} -->");

            var html = await System.IO.File.ReadAllTextAsync(indexPath);

            // 2. Load the HTML into HtmlAgilityPack
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // 3. Inject zone HTMLs
            await InjectZoneHtmlInRendererAsync(doc, "titlebar-zone", debugMessages);
            await InjectZoneHtmlInRendererAsync(doc, "nav-rail-zone", debugMessages);
            await InjectZoneHtmlInRendererAsync(doc, "content-zone", debugMessages);

            // Append debug messages before returning
            debugMessages.AppendLine("<!-- DEBUG END -->");
            var body = doc.DocumentNode.SelectSingleNode("//body");
            if (body != null)
            {
                body.AppendChild(doc.CreateComment(debugMessages.ToString()));
            }
            else
            {
                // If no body, append to the root document node
                doc.DocumentNode.AppendChild(doc.CreateComment(debugMessages.ToString()));
            }

            // 4. Return the composed HTML
            return doc.DocumentNode.OuterHtml;
        }

        private async Task InjectZoneHtmlInRendererAsync(HtmlDocument doc, string zoneId, StringBuilder debugMessages)
        {
            debugMessages.AppendLine($"<!-- Attempting to inject zone: {zoneId} -->");
            var placeholderElement = doc.DocumentNode.SelectSingleNode($"//div[@id='{zoneId}']");
            if (placeholderElement != null)
            {
                debugMessages.AppendLine($"<!-- Found placeholder for zone: {zoneId} -->");
                var fullZoneHtml = await _templates.GetZoneHtmlAsync(zoneId, debugMessages);
                if (!string.IsNullOrEmpty(fullZoneHtml))
                {
                    debugMessages.AppendLine($"<!-- Full zone HTML received for {zoneId}: {fullZoneHtml.Substring(0, Math.Min(fullZoneHtml.Length, 100))}... -->");
                    fullZoneHtml = fullZoneHtml.Replace("-->", "").Trim();
                    debugMessages.AppendLine($"<!-- Trimmed zone HTML for {zoneId}: {fullZoneHtml.Substring(0, Math.Min(fullZoneHtml.Length, 100))}... -->");

                    var fragmentDoc = new HtmlDocument();
                    fragmentDoc.LoadHtml(fullZoneHtml);
                    var fragmentRoot = fragmentDoc.DocumentNode.SelectSingleNode("//div");

                    if (fragmentRoot != null)
                    {
                        debugMessages.AppendLine($"<!-- Fragment root found for {zoneId}: {fragmentRoot.OuterHtml.Substring(0, Math.Min(fragmentRoot.OuterHtml.Length, 100))}... -->");
                        debugMessages.AppendLine($"<!-- Attempting to replace placeholder for {zoneId}... -->");
                        placeholderElement.ParentNode.ReplaceChild(fragmentRoot.CloneNode(true), placeholderElement);
                        debugMessages.AppendLine($"<!-- Placeholder replaced for {zoneId}. -->");
                    }
                    else
                    {
                        debugMessages.AppendLine($"<!-- ERROR: Fragment root NOT found for zone: {zoneId}. Full zone HTML: {fullZoneHtml} -->");
                    }
                }
                else
                {
                    debugMessages.AppendLine($"<!-- WARNING: Full zone HTML was EMPTY for zone: {zoneId} -->");
                }
            }
            else
            {
                debugMessages.AppendLine($"<!-- WARNING: Placeholder NOT found for zone: {zoneId} -->");
            }
        }

        // --- 2. DYNAMIC MANIFEST LOGIC (Recursion) ---

        public async Task<string> RenderManifestAsync(UiManifest manifest)
        {
            if (manifest.RootComponent == null) return "";
            return await BuildComponentHtmlAsync(manifest.RootComponent);
        }

        public async Task<string> BuildComponentHtmlAsync(ComponentDefinition component)
        {
            string baseHtml;

            // A. Resolve Template
            if (component.Type.ToLower() == "custom")
            {
                baseHtml = component.RawHtml ?? "<div>Error: No RawHtml provided</div>";
            }
            else
            {
                baseHtml = await _templates.GetTemplateHtmlAsync(component.Type);
            }

            // B. Hydrate (Identity & Content)
            var runtimeGuid = Guid.NewGuid().ToString();

            // Basic String Replacements
            baseHtml = baseHtml.Replace("{{UI_COMPONENT_ID}}", runtimeGuid);
            baseHtml = baseHtml.Replace("{{CONTENT}}", component.Content ?? "");

            // C. Load into Parser for Structure Manipulation (Attributes & Children)
            var doc = new HtmlDocument();
            doc.LoadHtml(baseHtml);
            var rootNode = doc.DocumentNode.FirstChild; // The wrapper div

            if (rootNode == null) return baseHtml; // Fallback

            // Apply Attributes (if any)
            if (component.Attributes != null)
            {
                foreach (var attr in component.Attributes)
                {
                    rootNode.SetAttributeValue(attr.Key, attr.Value);
                }
            }

            // D. Process Children (Recursion)
            if (component.Children != null && component.Children.Count > 0)
            {
                // 1. Find the default mount point
                var mountPoint = rootNode.SelectSingleNode(".//*[@data-mount-point='default']")
                                 ?? rootNode; // Fallback to root if no mount point defined

                if (rootNode.GetAttributeValue("data-mount-point", "") == "default")
                {
                    mountPoint = rootNode;
                }

                if (mountPoint != null)
                {
                    foreach (var child in component.Children)
                    {
                        var childHtml = await BuildComponentHtmlAsync(child);
                        var childNode = HtmlNode.CreateNode(childHtml);
                        mountPoint.AppendChild(childNode);
                    }
                }
            }

            return doc.DocumentNode.OuterHtml;
        }
    }
}