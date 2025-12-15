using HtmlAgilityPack;
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

        public UiRenderer(UiTemplateService templates)
        {
            _templates = templates;
        }

        // --- 1. INITIAL LOAD LOGIC (Index + Zones) ---

        public async Task<string> GetComposedIndexPageAsync()
        {
            // 1. Load shell
            // Note: We assume the Host passes the correct root path to TemplateService
            // Here we need to read index.html specifically.
            // For simplicity, let's assume we read it via IO here or inject a path provider.
            // Pseudo-code implementation:
            var indexPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "index.html");
            if (!System.IO.File.Exists(indexPath)) return "Error: index.html not found";

            var indexHtml = await System.IO.File.ReadAllTextAsync(indexPath);

            // 2. Regex Discovery: var regex = new Regex(@"");
            var regex = new Regex(@"");
            var processedHtml = await regex.ReplaceAsync(indexHtml, async (match) =>
            {
                var zoneName = match.Groups[1].Value.Trim(); // e.g., "TITLEBAR"

                // 3. Load Zone File
                var zoneContent = await _templates.GetZoneHtmlAsync(zoneName);

                // 4. Assign Identity (GUID)
                // Zones get a generic ID or a specific System ID. Let's create a fresh one.
                var zoneGuid = Guid.NewGuid().ToString();

                // Replace Token
                return zoneContent.Replace("{{UI_COMPONENT_ID}}", zoneGuid);
            });

            return processedHtml;
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

    // Helper for async regex
    public static class RegexExtensions
    {
        public static async Task<string> ReplaceAsync(this Regex regex, string input, Func<Match, Task<string>> replacementFn)
        {
            var sb = new StringBuilder();
            var lastIndex = 0;
            foreach (Match match in regex.Matches(input))
            {
                sb.Append(input, lastIndex, match.Index - lastIndex);
                sb.Append(await replacementFn(match));
                lastIndex = match.Index + match.Length;
            }
            sb.Append(input, lastIndex, input.Length - lastIndex);
            return sb.ToString();
        }
    }
}