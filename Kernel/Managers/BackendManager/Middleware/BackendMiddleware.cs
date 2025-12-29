using Core.Backend.Contracts;
using Core.Messages;
using Core.Pipeline; // For RequestContext
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace BackendManager.Middleware
{
    /// <summary>
    /// Middleware to handle backend API requests
    /// </summary>
    /// <param name="next"></param>
    public class BackendMiddleware(RequestDelegate next)
    {
        private readonly RequestDelegate _next = next;
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Invoke the Middleware
        /// </summary>
        /// <param name="context"></param>
        /// <param name="bus"></param>
        /// <returns></returns>
        public async Task InvokeAsync(HttpContext context, IBackendPipeline bus)
        {
            // 1. Check for API Endpoint
            if (context.Request.Method == "POST" &&
                context.Request.Path.Equals("/api/backend/dispatch", StringComparison.OrdinalIgnoreCase))
            {
                await HandleDispatchAsync(context, bus);
                return;
            }

            // 2. Pass to next (or 404 if nothing matches)
            await _next(context);
        }

        private static async Task HandleDispatchAsync(HttpContext context, IBackendPipeline bus)
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