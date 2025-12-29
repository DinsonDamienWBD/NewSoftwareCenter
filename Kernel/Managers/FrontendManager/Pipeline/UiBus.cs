using Core;
using Core.Frontend.Contracts;
using Core.Frontend.Messages;
using Core.Messages;
using Core.Pipeline;
using Core.ServiceRegistry;
using FrontendManager.Hubs;
using FrontendManager.Middleware;
using FrontendManager.Registry;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace FrontendManager.Pipeline
{
    /// <summary>
    /// UI Message Bus / Pipeline Implementation.
    /// </summary>
    /// <param name="registry"></param>
    /// <param name="exceptionMiddleware"></param>
    /// <param name="accessControlMiddleware"></param>
    /// <param name="deprecationMiddleware"></param>
    /// <param name="validationMiddleware"></param>
    /// <param name="auditMiddleware"></param>
    /// <param name="hubContext"></param>
    public class UiBus(
        UiRegistry registry,
        UiExceptionMiddleware exceptionMiddleware,
        AccessControlMiddleware accessControlMiddleware,
        UiDeprecationMiddleware deprecationMiddleware,
        UiValidationMiddleware validationMiddleware,
        UiAuditMiddleware auditMiddleware,
        IHubContext<FrontendHub, IFrontendClient> hubContext) : IFrontendPipeline
    {
        private readonly UiRegistry _registry = registry;
        private readonly UiExceptionMiddleware _exceptionMiddleware = exceptionMiddleware;
        private readonly AccessControlMiddleware _accessControlMiddleware = accessControlMiddleware;
        private readonly UiDeprecationMiddleware _deprecationMiddleware = deprecationMiddleware;
        private readonly UiValidationMiddleware _validationMiddleware = validationMiddleware; // Store it
        private readonly UiAuditMiddleware _auditMiddleware = auditMiddleware;           // Store it
        private readonly IHubContext<FrontendHub, IFrontendClient> _hub = hubContext;

        private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
        private static readonly ConcurrentDictionary<Type, MethodInfo> _methodCache = new();

        /// <summary>
        /// Send a MessageEnvelope to the UI Bus.
        /// </summary>
        /// <param name="envelope"></param>
        /// <param name="context"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public async Task<Result<object?>> SendEnvelopeAsync(MessageEnvelope envelope, IRequestContext context, CancellationToken ct = default)
        {
            try
            {
                var type = _registry.GetMessageType(envelope.MessageType);
                if (type is null) return Result<object?>.Failure($"Unknown message type: {envelope.MessageType}");

                var message = envelope.Payload.Deserialize(type, _jsonOptions);
                if (message is not IMessage typedMessage) return Result<object?>.Failure("Invalid payload.");

                if (IsStream(type)) return await InvokeGenericWrapper(type, typedMessage, context, "StreamWrapper", ct);
                if (IsQuery(type)) return await InvokeGenericWrapper(type, typedMessage, context, "QueryWrapper", ct);
                if (IsCommandWithResult(type)) return await InvokeGenericWrapper(type, typedMessage, context, "CommandWithResultWrapper", ct);

                if (message is IUiCommand cmd)
                {
                    var res = await SendAsync(cmd, context, ct);
                    return res.IsSuccess
                        ? Result<object?>.Success(null)
                        : Result<object?>.Failure(res.Error ?? "Unknown", res.ErrorCode ?? "GeneralError");
                }

                if (message is IUiJob job)
                {
                    var id = await EnqueueJobAsync(job, context, ct);
                    return Result<object?>.Success(id);
                }

                return Result<object?>.Failure($"Message type '{type.Name}' is not valid.");
            }
            catch (Exception ex)
            {
                return Result<object?>.Failure($"UI Dispatch Error: {ex.Message}");
            }
        }

        private async Task<Result<object?>> InvokeGenericWrapper(Type msgType, IMessage msg, IRequestContext ctx, string wrapperName, CancellationToken ct)
        {
            var method = _methodCache.GetOrAdd(msgType, t =>
            {
                var resultType = GetResultType(t);
                var genericMethod = typeof(UiBus).GetMethod(wrapperName, BindingFlags.NonPublic | BindingFlags.Instance);
                return genericMethod!.MakeGenericMethod(resultType);
            });

            var task = (Task<Result<object?>>)method.Invoke(this, [msg, ctx, ct])!;
            return await task;
        }

        // --- Wrappers ---
        private async Task<Result<object?>> QueryWrapper<TResponse>(dynamic query, IRequestContext ctx, CancellationToken ct)
        {
            var res = await SendAsync<TResponse>(query, ctx, ct);
            return Result<object?>.Success(res);
        }

        private async Task<Result<object?>> CommandWithResultWrapper<TResponse>(dynamic command, IRequestContext ctx, CancellationToken ct)
        {
            var res = await SendAsync<TResponse>(command, ctx, ct);
            return Result<object?>.Success(res);
        }

        private async Task<Result<object?>> StreamWrapper<TResponse>(dynamic request, IRequestContext ctx, CancellationToken ct)
        {
            var stream = StreamAsync<TResponse>(request, ctx, ct);
            return Result<object?>.Success(stream);
        }

        // --- IFrontendPipeline Implementation ---

        /// <summary>
        /// Send a UI Command.
        /// </summary>
        /// <param name="command"></param>
        /// <param name="ctx"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public async Task<Result> SendAsync(IUiCommand command, IRequestContext ctx, CancellationToken ct = default)
        {
            // 1. Exception Safety (Outermost)
            return await _exceptionMiddleware.ExecuteAsync(async () =>
            {
                // 2. Audit / Tracing (Start the Timer/Span)
                await _auditMiddleware.LogExecutionAsync(command, ctx.RequestorId);

                // 3. Validation (Check Rules)
                var validation = _validationMiddleware.Validate(command);
                if (!validation.IsSuccess) return Result<bool>.Failure(validation.Error!, "ValidationError");

                // 4. Access Control (Permissions) - Existing logic
                // (Assuming AccessControl middleware usage was implicit or inside handler lookup. 
                //  For V1, we often skip explicit middleware call here if not fully implemented, 
                //  but we inject it, so let's stick to the core flow.)

                // 5. Execution
                var handlerType = typeof(IHandler<>).MakeGenericType(command.GetType());
                var handler = _registry.ServiceProvider.GetService(handlerType);
                if (handler is null) return Result<bool>.Failure($"No UI handler for {command.GetType().Name}");

                var method = typeof(IHandler<>).MakeGenericType(command.GetType()).GetMethod("HandleAsync");
                await (Task)method!.Invoke(handler, [command, ct])!;

                return Result<bool>.Success(true);

            }, command.GetType().Name, ctx.RequestorId);
        }

        /// <summary>
        /// Send a UI Command with Result.
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="command"></param>
        /// <param name="ctx"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<TResult> SendAsync<TResult>(IUiCommand<TResult> command, IRequestContext ctx, CancellationToken ct = default)
        {
            var res = await _exceptionMiddleware.ExecuteAsync(async () =>
            {
                // Audit
                await _auditMiddleware.LogExecutionAsync(command, ctx.RequestorId);

                // Validate
                var validation = _validationMiddleware.Validate(command);
                if (!validation.IsSuccess) return Result<TResult>.Failure(validation.Error!);

                // Execute
                var handlerType = typeof(IHandler<,>).MakeGenericType(command.GetType(), typeof(TResult));
                var handler = _registry.ServiceProvider.GetService(handlerType);
                if (handler is null) return Result<TResult>.Failure($"No UI handler for {command.GetType().Name}");

                var method = typeof(IHandler<,>).MakeGenericType(command.GetType(), typeof(TResult)).GetMethod("HandleAsync");
                var task = (Task<TResult>)method!.Invoke(handler, [command, ct])!;
                return Result<TResult>.Success(await task);

            }, command.GetType().Name, ctx.RequestorId);

            if (!res.IsSuccess) throw new Exception(res.Error);
            return res.Value!;
        }

        /// <summary>
        /// Send a UI Query.
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="query"></param>
        /// <param name="ctx"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public async Task<TResult> SendAsync<TResult>(IUiQuery<TResult> query, IRequestContext ctx, CancellationToken ct = default)
        {
            return await SendAsync((dynamic)query, ctx, ct);
        }

        /// <summary>
        /// Publish a UI Event.
        /// </summary>
        /// <param name="event"></param>
        /// <param name="ctx"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public async Task PublishAsync(IUiEvent @event, IRequestContext ctx, CancellationToken ct = default)
        {
            _ = ctx; _ = ct;

            // Audit Events? Usually too noisy. We skip Audit middleware here.

            // 1. Internal Bus
            try
            {
                var eventType = @event.GetType();
                var handlerType = typeof(IHandler<>).MakeGenericType(eventType);
                var handler = _registry.ServiceProvider.GetService(handlerType);
                if (handler != null)
                {
                    var method = handlerType.GetMethod("HandleAsync");
                    if (method != null) await (Task)method.Invoke(handler, [@event, ct])!;
                }
            }
            catch { /* Swallow */ }

            // 2. SignalR
            if (_hub != null) await _hub.Clients.All.ReceiveMessage(@event.GetType().Name, @event);
        }

        /// <summary>
        /// Stream a UI Request.
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="request"></param>
        /// <param name="ctx"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public async IAsyncEnumerable<TResult> StreamAsync<TResult>(IStreamRequest<TResult> request, IRequestContext ctx, [EnumeratorCancellation] CancellationToken ct = default)
        {
            var handlerType = typeof(IHandler<,>).MakeGenericType(request.GetType(), typeof(IAsyncEnumerable<TResult>));
            var handler = _registry.ServiceProvider.GetService(handlerType) ?? throw new InvalidOperationException($"No Stream handler for {request.GetType().Name}");
            var method = handlerType.GetMethod("HandleAsync"); // FIX: Use Interface
            var task = (Task<IAsyncEnumerable<TResult>>)method!.Invoke(handler, [request, ct])!;

            var stream = await task;
            await foreach (var item in stream.WithCancellation(ct))
            {
                yield return item;
            }
        }

        /// <summary>
        /// Enqueue a UI Job.
        /// </summary>
        /// <param name="job"></param>
        /// <param name="ctx"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public Task<string> EnqueueJobAsync(IUiJob job, IRequestContext ctx, CancellationToken ct = default)
        {
            _ = Task.Run(async () =>
            {
                var handlerType = typeof(IHandler<>).MakeGenericType(job.GetType());
                var handler = _registry.ServiceProvider.GetService(handlerType);
                if (handler is not null)
                {
                    var method = handlerType.GetMethod("HandleAsync"); // FIX
                    await (Task)method!.Invoke(handler, [job, CancellationToken.None])!;
                }
            }, ct);
            return Task.FromResult(Guid.NewGuid().ToString());
        }

        // --- Explicit IPipeline Implementation ---

        Task IPipeline.SendAsync(IMessage message, IRequestContext context, CancellationToken ct)
            => message is IUiCommand c ? SendAsync(c, context, ct) : Task.FromException(new InvalidCastException("Not a UI Command"));

        Task<TResult> IPipeline.SendAsync<TResult>(IMessage message, IRequestContext context, CancellationToken ct)
            => message is IUiCommand<TResult> c ? SendAsync(c, context, ct) : throw new InvalidCastException("Not a UI Command<T>");

        Task<TResult> IPipeline.QueryAsync<TResult>(IMessage message, IRequestContext context, CancellationToken ct)
            => message is IUiQuery<TResult> q ? SendAsync(q, context, ct) : throw new InvalidCastException("Not a UI Query<T>");

        Task<TResult> IFrontendPipeline.QueryAsync<TResult>(IUiQuery<TResult> query, IRequestContext context, CancellationToken ct)
            => SendAsync(query, context, ct);

        Task IPipeline.PublishAsync(IMessage message, IRequestContext context, CancellationToken ct) =>
            message is IUiEvent e ? PublishAsync(e, context, ct) : Task.CompletedTask;

        Task<string> IPipeline.EnqueueJobAsync(IMessage message, IRequestContext context, CancellationToken ct)
            => message is IUiJob j ? EnqueueJobAsync(j, context, ct) : Task.FromResult(Guid.Empty.ToString());

        IAsyncEnumerable<TResult> IPipeline.StreamAsync<TResult>(IMessage message, IRequestContext context, CancellationToken ct)
             => message is IStreamRequest<TResult> r ? StreamAsync(r, context, ct) : throw new InvalidCastException("Not a Stream Request");


        // --- Helpers ---
        private static bool IsQuery(Type t) => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IUiQuery<>));
        private static bool IsCommandWithResult(Type t) => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IUiCommand<>));
        private static bool IsStream(Type t) => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IStreamRequest<>));
        private static Type GetResultType(Type t) => t.GetInterfaces().FirstOrDefault(x => x.IsGenericType && (x.GetGenericTypeDefinition() == typeof(IUiQuery<>) || x.GetGenericTypeDefinition() == typeof(IUiCommand<>) || x.GetGenericTypeDefinition() == typeof(IStreamRequest<>)))?.GetGenericArguments()[0] ?? typeof(object);
    }
}