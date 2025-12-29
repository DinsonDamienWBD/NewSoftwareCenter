using Core;
using Core.Frontend.Contracts;
using Core.Frontend.Contracts.Models;
using Core.Frontend.Messages;
using Core.ServiceRegistry;
using FrontendManager.Hubs;
using FrontendManager.Registry;
using Microsoft.AspNetCore.SignalR;
using System.Text;
using System.Text.RegularExpressions;

namespace FrontendManager.Handlers
{
    /// <summary>
    /// Composes and injects UI elements based on FrontendManifest or Raw HTML.
    /// </summary>
    /// <param name="registry"></param>
    /// <param name="hub"></param>
    public partial class UiCompositionHandler(
        UiRegistry registry,
        IHubContext<FrontendHub, IFrontendClient> hub) :
        IHandler<InjectUiCommand, Result<List<string>>>
    {
        private readonly UiRegistry _registry = registry;
        private readonly IHubContext<FrontendHub, IFrontendClient> _hub = hub;

        // FIX: Use standard static readonly field to match usage 'TemplateRegex.Matches'
        // This removes the conflict with the [GeneratedRegex] method.
        private static readonly Regex TemplateRegex = UiTemplateRegex();

        /// <summary>
        /// Handles the InjectUiCommand to compose and inject UI elements.
        /// </summary>
        /// <param name="command"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public async Task<Result<List<string>>> HandleAsync(InjectUiCommand command, CancellationToken ct)
        {
            var generatedIds = new List<string>();
            var ownerId = command.Metadata.TryGetValue("OwnerId", out var oid) ? oid.ToString() : "Unknown";

            // 1. Validate Target Exists (unless it's a root zone injection)
            // In V1 we skip strict validation to allow 'Fire and Forget', but ideally we check _registry.GetUi(command.TargetGuid)

            string finalHtml;

            // CASE 1: Raw HTML
            if (!string.IsNullOrEmpty(command.RawHtml))
            {
                var newId = Guid.NewGuid().ToString();
                generatedIds.Add(newId);

                // We wrap raw HTML in a tracking div so we can manage it later
                finalHtml = $@"<div data-id=""{newId}"" data-owner=""{ownerId}"">{command.RawHtml}</div>";

                // Register in Memory (for persistence on refresh)
                _registry.RegisterUi(new UiRegistrationEntry
                {
                    Id = newId.ToString(),
                    OwnerId = ownerId!,
                    Type = "Widget",
                    Content = finalHtml,
                    MountPointId = command.MountPoint,
                    // Link to parent so ComposeShell can find it on reload (Logic needed in Middleware to handle nested parents)
                    // For V1, we mainly rely on SignalR for dynamic elements.
                });
            }
            // CASE 2: Manifest (Template Based)
            else if (command.Manifest != null)
            {
                // Load Templates
                var templateLib = _registry.GetUi("StandardTemplates")?.Content ?? "";
                var templates = ParseTemplates(templateLib);

                // Recursively Build
                finalHtml = ProcessManifest(command.Manifest, templates, generatedIds, ownerId!);
            }
            else
            {
                return Result<List<string>>.Failure("Command must contain either Manifest or RawHtml.");
            }

            // 2. Push to Client (The "Live Wire")
            // The client JS will find [data-id='TargetGuid'] -> [data-mount-point='MountPoint'] and append html
            await _hub.Clients.All.InjectFragment(command.TargetGuid, command.MountPoint, finalHtml);

            return Result<List<string>>.Success(generatedIds);
        }

        private string ProcessManifest(FrontendManifest manifest, Dictionary<string, string> templates, List<string> ids, string ownerId)
        {
            // A. Identity
            var newId = Guid.NewGuid().ToString();
            ids.Add(newId);
            manifest.ElementId = newId; // Update the manifest with the assigned ID

            // B. Template Lookup
            string templateHtml;
            if (templates.TryGetValue(manifest.ComponentType, out var tpl))
            {
                templateHtml = tpl;
            }
            else
            {
                // Fallback for unknown types
                templateHtml = $"<div class='error-placeholder'>Unknown Type: {manifest.ComponentType}</div>";
            }

            // C. Hydration (Replace Tokens)
            var sb = new StringBuilder(templateHtml);
            sb.Replace("{{UI_COMPONENT_ID}}", newId.ToString());
            sb.Replace("{{CONTENT}}", manifest.Properties?.GetValueOrDefault("content")?.ToString() ?? "");

            // Handle specific properties (e.g. {{ICON}}, {{TITLE}}) logic would go here
            // For V1, we stick to generic {{CONTENT}}

            // D. Recursion (Process Children)
            if (manifest.Children != null && manifest.Children.Count > 0)
            {
                var childrenHtml = new StringBuilder();
                foreach (var child in manifest.Children)
                {
                    childrenHtml.Append(ProcessManifest(child, templates, ids, ownerId));
                }
                // Inject children into the {{CONTENT}} or a specific {{CHILDREN}} slot
                // For simplicity in V1, we append children to the content area
                sb.Replace("{{CHILDREN}}", childrenHtml.ToString());
            }
            else
            {
                sb.Replace("{{CHILDREN}}", "");
            }

            var composedHtml = sb.ToString();

            // E. Register
            _registry.RegisterUi(new UiRegistrationEntry
            {
                Id = newId.ToString(),
                OwnerId = ownerId,
                Type = "DynamicWidget",
                ComponentType = manifest.ComponentType,
                Content = composedHtml,
                IsVisible = true
            });

            return composedHtml;
        }

        private static Dictionary<string, string> ParseTemplates(string html)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var matches = TemplateRegex.Matches(html);
            foreach (Match match in matches)
            {
                // Group 1: ID (tpl-button), Group 2: Content
                dict[match.Groups[1].Value] = match.Groups[2].Value;
            }
            return dict;
        }

        [GeneratedRegex(@"<template\s+id=""([^""]+)""[^>]*>([\s\S]*?)<\/template>", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
        private static partial Regex UiTemplateRegex();
    }
}