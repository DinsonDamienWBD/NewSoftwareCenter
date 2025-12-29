using Core.Backend.Contracts;
using Core.Frontend.Contracts;
using Microsoft.AspNetCore.Http;

namespace Host.Infrastructure
{
    /// <summary>
    /// Middleware to unify dispatching of commands to backend or frontend handlers based on command type.
    /// </summary>
    public class UnifiedDispatchMiddleware
    {
        private readonly RequestDelegate _next;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="next"></param>
        public UnifiedDispatchMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        /// <summary>
        /// InvokeAsync method to process incoming HTTP requests.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="backend"></param>
        /// <param name="frontend"></param>
        /// <returns></returns>
        public async Task InvokeAsync(HttpContext context, IBackendRegistry backend, IFrontendRegistry frontend)
        {
            // 1. FILTER: Only intercept POST /api/dispatch
            if (!context.Request.Path.Equals("/api/dispatch", StringComparison.OrdinalIgnoreCase) ||
                context.Request.Method != "POST")
            {
                await _next(context);
                return;
            }

            // 2. READ HEADER (Clean & Safe)
            // We read the type from the header so we don't have to touch the Body stream.
            if (!context.Request.Headers.TryGetValue("X-Command-Type", out var commandTypeValues))
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new { Error = "Missing 'X-Command-Type' header." });
                return;
            }

            string commandType = commandTypeValues.ToString();

            // 3. ROUTE
            if (backend.GetMessageType(commandType) != null)
            {
                Console.WriteLine($"[Middleware] Routing '{commandType}' to BACKEND");
                context.Request.Path = "/api/backend/dispatch";
            }
            else if (frontend.GetMessageType(commandType) != null)
            {
                Console.WriteLine($"[Middleware] Routing '{commandType}' to FRONTEND");
                context.Request.Path = "/api/frontend/dispatch";
            }
            else
            {
                context.Response.StatusCode = 404;
                await context.Response.WriteAsJsonAsync(new { Error = $"Command '{commandType}' is not registered." });
                return;
            }

            // 4. FORWARD
            await _next(context);
        }
    }
}