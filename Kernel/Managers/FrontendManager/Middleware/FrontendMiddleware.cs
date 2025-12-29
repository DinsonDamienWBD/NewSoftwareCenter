using Core.Frontend.Contracts;
using Core.Messages;
using Core.Pipeline;
using Microsoft.AspNetCore.Http;
using System.Text;
using System.Text.Json;

namespace FrontendManager.Middleware
{
    /// <summary>
    /// Frontend Middleware to serve Shell, Styles, and handle Dispatch requests.
    /// </summary>
    /// <param name="next"></param>
    /// <param name="registry"></param>
    public class FrontendMiddleware(RequestDelegate next, IFrontendRegistry registry)
    {
        private readonly RequestDelegate _next = next;
        private readonly IFrontendRegistry _registry = registry;
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// InvokeAsync method to handle incoming HTTP requests.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="registry"></param>
        /// <param name="bus"></param>
        /// <returns></returns>
        public async Task InvokeAsync(HttpContext context, IFrontendRegistry registry, IFrontendPipeline bus)
        {
            var path = context.Request.Path.Value?.ToLower() ?? "";

            // 1. Check if this is a request for the App Root
            if (context.Request.Method == "GET" && (context.Request.Path == "/" || context.Request.Path == "/index.html"))
            {
                // 2. Retrieve the "Shell" registered by SystemModule
                var shell = _registry.GetUi("Shell");

                if (shell != null && !string.IsNullOrEmpty(shell.Content))
                {
                    context.Response.ContentType = "text/html";
                    await context.Response.WriteAsync(shell.Content);
                    return; // Stop pipeline here, we served the page
                }
            }

            // 3. Handle Dispatch (POST) - NEW
            if (context.Request.Method == "POST" && path == "/api/frontend/dispatch")
            {
                await HandleDispatchAsync(context, bus); // Logic identical to Backend logic above
                return;
            }

            // 3.Otherwise, pass to next(Backend or 404)
            await _next(context);
        }

        private string ComposeShell(string shellHtml)
        {
            var allUi = _registry.GetAllUi().Where(x => x.IsVisible).ToList();

            // --- A. INJECT STYLES ---
            // Find </head> and insert styles before it
            var styles = allUi.Where(x => x.Type == "Style").OrderBy(x => x.Priority).ToList();
            if (styles.Count > 0)
            {
                var headCloseIndex = shellHtml.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
                if (headCloseIndex > -1)
                {
                    var sb = new StringBuilder();
                    foreach (var style in styles)
                    {
                        sb.AppendLine($"<style data-id='{style.Id}'>{style.Content}</style>");
                    }
                    shellHtml = shellHtml.Insert(headCloseIndex, sb.ToString());
                }
            }

            // --- B. INJECT WIDGETS INTO ZONES ---
            // We sort by Priority Descending because we insert at the "top" of the container (after the opening tag).
            // Inserting High Priority last makes it appear first.
            var widgets = allUi
                .Where(x => x.Type == "Widget" && !string.IsNullOrEmpty(x.ZoneId))
                .OrderByDescending(x => x.Priority)
                .ToList();

            foreach (var widget in widgets)
            {
                // Strategy: Find the container by ID (e.g. id="zone-nav")
                // And insert the content immediately after the opening tag >

                var searchKey = $"id=\"{widget.ZoneId}\"";
                var idIndex = shellHtml.IndexOf(searchKey, StringComparison.OrdinalIgnoreCase);

                if (idIndex > -1)
                {
                    // Find the end of the opening tag (the next '>')
                    var tagEndIndex = shellHtml.IndexOf(">", idIndex);

                    if (tagEndIndex > -1)
                    {
                        // Inject!
                        // We wrap it in a div/placeholder if needed, but for V1 we inject raw content
                        // to keep the layout grid clean.
                        shellHtml = shellHtml.Insert(tagEndIndex + 1, $"\n{widget.Content}\n");
                    }
                }
            }

            return shellHtml;
        }

        private static async Task ServeShellAsync(HttpContext context, IFrontendRegistry registry)
        {
            var shellEntry = registry.GetUi("Shell");
            if (shellEntry == null)
            {
                context.Response.ContentType = "text/html";
                await context.Response.WriteAsync("<h1>Kernel Error</h1><p>No Shell registered. Is SystemModule loaded?</p>");
                return;
            }

            var html = shellEntry.Content;

            // Simple V1 Injection: Find <div id="zone-x"> and push content inside.
            var widgets = registry.GetAllUi()
                .Where(x => !string.IsNullOrEmpty(x.ZoneId) && x.IsVisible)
                .OrderBy(x => x.Priority)
                .ToList();

            foreach (var widget in widgets)
            {
                // We target the opening tag: <div id="zone-nav">
                // And replace it with: <div id="zone-nav">...Content...
                var target = $"id=\"{widget.ZoneId}\">";
                var replacement = $"{target}\n{widget.Content}\n";

                // Note: string.Replace replaces ALL instances. 
                // Ensure Zone IDs are unique in your index.html.
                html = html.Replace(target, replacement);
            }

            context.Response.ContentType = "text/html";
            await context.Response.WriteAsync(html);
        }

        private static async Task ServeStylesAsync(HttpContext context, IFrontendRegistry registry)
        {
            var sb = new StringBuilder();

            var styles = registry.GetAllUi()
                .Where(x => x.Type == "Style")
                .OrderBy(x => x.Priority);

            foreach (var style in styles)
            {
                sb.AppendLine($"/* Source: {style.OwnerId} ({style.Id}) */");
                sb.AppendLine(style.Content);
                sb.AppendLine();
            }

            context.Response.ContentType = "text/css";
            await context.Response.WriteAsync(sb.ToString());
        }

        // NEW: Add the same HandleDispatchAsync logic as FrontendMiddleware
        private static async Task HandleDispatchAsync(HttpContext context, IFrontendPipeline bus)
        {
            // A. Deserialize the Envelope
            MessageEnvelope? envelope;
            try
            {
                envelope = await JsonSerializer.DeserializeAsync<MessageEnvelope>(
                    context.Request.Body,
                    _jsonOptions
                );
            }
            catch (JsonException)
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new { Error = "Invalid JSON Envelope" });
                return;
            }

            if (envelope == null)
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new { Error = "Empty Payload" });
                return;
            }

            // B. Execute via Bus
            // In the future, we extract the User from context.User for the RequestContext
            var result = await bus.SendEnvelopeAsync(envelope, RequestContext.System);

            // C. Write Response
            if (result.IsSuccess)
            {
                context.Response.StatusCode = 200;
                // If the command returned data (Command<T>), send it back. Otherwise send 200 OK.
                if (result.Value != null)
                {
                    await context.Response.WriteAsJsonAsync(result.Value);
                }
            }
            else
            {
                context.Response.StatusCode = 400; // Or 500 depending on error type
                await context.Response.WriteAsJsonAsync(new
                {
                    result.Error,
                    Code = result.ErrorCode
                });
            }
        }
    }
}