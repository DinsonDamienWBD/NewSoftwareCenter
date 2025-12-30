using Manager.Middleware;
using Manager.Registry;
using Core;
using Core.Backend.Contracts;
using Core.Backend.Messages;
using Core.Frontend.Messages;
using Core.Messages;
using Core.Pipeline;
using Core.ServiceRegistry;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;

namespace Manager.Pipeline
{
    /// <summary>
    /// Backend Bus Implementation with Middleware Pipeline.
    /// </summary>
    /// <param name="registry"></param>
    /// <param name="exceptionMiddleware"></param>
    /// <param name="validationMiddleware"></param>
    /// <param name="auditMiddleware"></param>
    /// <param name="deprecationMiddleware"></param>
    public class BackendBus(
        ServiceRegistry registry,
        ExceptionMiddleware exceptionMiddleware,
        ValidationMiddleware validationMiddleware,
        AuditMiddleware auditMiddleware,
        DeprecationMiddleware deprecationMiddleware) : IBackendPipeline
    {
        private readonly ServiceRegistry _registry = registry;
        private readonly ExceptionMiddleware _exceptionMiddleware = exceptionMiddleware;
        private readonly ValidationMiddleware _validationMiddleware = validationMiddleware;
        private readonly AuditMiddleware _auditMiddleware = auditMiddleware;
        private readonly DeprecationMiddleware _deprecationMiddleware = deprecationMiddleware;

        private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
        private static readonly ConcurrentDictionary<Type, MethodInfo> _sendMethods = new();

        /// <summary>
        /// Sends a MessageEnvelope to the BackendBus, deserializing and dispatching to the appropriate handler.
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
                if (message is not IMessage typedMessage) return Result<object?>.Failure("Invalid payload structure.");

                if (IsQuery(type))
                    return await InvokeGenericWrapper(type, typedMessage, context, "QueryWrapper", ct);

                if (IsCommandWithResult(type))
                    return await InvokeGenericWrapper(type, typedMessage, context, "CommandWithResultWrapper", ct);

                if (message is ICommand voidCommand)
                {
                    var res = await SendAsync(voidCommand, context, ct);
                    return res.IsSuccess
                        ? Result<object?>.Success(null)
                        : Result<object?>.Failure(res.Error ?? "Unknown", res.ErrorCode ?? "GeneralError");
                }

                return Result<object?>.Failure($"Message type '{type.Name}' is not a valid Command or Query.");
            }
            catch (Exception ex)
            {
                return Result<object?>.Failure($"Dispatch Error: {ex.Message}");
            }
        }

        private async Task<Result<object?>> InvokeGenericWrapper(Type messageType, IMessage message, IRequestContext ctx, string wrapperName, CancellationToken ct)
        {
            var method = _sendMethods.GetOrAdd(messageType, t =>
            {
                var resultType = GetResultType(t);
                var genericMethod = typeof(BackendBus).GetMethod(wrapperName, BindingFlags.NonPublic | BindingFlags.Instance) ?? throw new InvalidOperationException($"Wrapper {wrapperName} not found.");
                return genericMethod.MakeGenericMethod(resultType);
            });

            var task = (Task<Result<object?>>)method.Invoke(this, [message, ctx, ct])!;
            return await task;
        }

        private async Task<Result<object?>> QueryWrapper<TResponse>(dynamic query, IRequestContext ctx, CancellationToken ct)
        {
            try { var res = await QueryAsync<TResponse>(query, ctx, ct); return Result<object?>.Success(res); }
            catch (Exception ex) { return Result<object?>.CriticalError(ex.Message); }
        }

        private async Task<Result<object?>> CommandWithResultWrapper<TResponse>(dynamic command, IRequestContext ctx, CancellationToken ct)
        {
            try { var res = await SendAsync<TResponse>(command, ctx, ct); return Result<object?>.Success(res); }
            catch (Exception ex) { return Result<object?>.CriticalError(ex.Message); }
        }

        // --- IBackendPipeline Implementation ---

        /// <summary>
        /// Sends a Command to the BackendBus, processing through middleware and dispatching to the appropriate handler.
        /// </summary>
        /// <param name="command"></param>
        /// <param name="ctx"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public async Task<Result> SendAsync(ICommand command, IRequestContext ctx, CancellationToken ct = default)
        {
            var wrapperResult = await _exceptionMiddleware.ExecuteAsync(async () =>
            {
                var valResult = _validationMiddleware.Validate(command);
                if (!valResult.IsSuccess) return Result<bool>.Failure(valResult.Error!, valResult.ErrorCode ?? "ValidationError");

                await _auditMiddleware.LogExecutionAsync(command, ctx.RequestorId);

                // FIX: Use Interface Type to avoid AmbiguousMatchException
                var handlerType = typeof(IHandler<>).MakeGenericType(command.GetType());
                var handler = _registry.ServiceProvider.GetService(handlerType);

                if (handler is null) return Result<bool>.Failure($"No handler registered for '{command.GetType().Name}'.");

                var method = handlerType.GetMethod("HandleAsync"); // Unambiguous on the interface
                await (Task)method!.Invoke(handler, [command, ct])!;

                return Result<bool>.Success(true);
            }, command.GetType().Name, ctx.RequestorId);

            return wrapperResult.IsSuccess
                ? Result.Success()
                : Result.Failure(wrapperResult.Error ?? "Error", wrapperResult.ErrorCode ?? "GeneralError");
        }

        /// <summary>
        /// Sends a Command with Result to the BackendBus, processing through middleware and dispatching to the appropriate handler.
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="command"></param>
        /// <param name="ctx"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<TResult> SendAsync<TResult>(ICommand<TResult> command, IRequestContext ctx, CancellationToken ct = default)
        {
            var res = await _exceptionMiddleware.ExecuteAsync(async () =>
            {
                var valResult = _validationMiddleware.Validate(command);
                if (!valResult.IsSuccess) return Result<TResult>.Failure(valResult.Error!);

                await _auditMiddleware.LogExecutionAsync(command, ctx.RequestorId);

                var handlerType = typeof(IHandler<,>).MakeGenericType(command.GetType(), typeof(TResult));
                var handler = _registry.ServiceProvider.GetService(handlerType);

                if (handler is null) return Result<TResult>.Failure($"No handler registered for '{command.GetType().Name}'.");

                var method = handlerType.GetMethod("HandleAsync"); // Unambiguous
                var task = (Task<TResult>)method!.Invoke(handler, [command, ct])!;
                return Result<TResult>.Success(await task);

            }, command.GetType().Name, ctx.RequestorId);

            if (!res.IsSuccess) throw new Exception(res.Error);
            return res.Value!;
        }

        /// <summary>
        /// Queries the BackendBus, processing through middleware and dispatching to the appropriate handler.
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="query"></param>
        /// <param name="ctx"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<TResult> QueryAsync<TResult>(IQuery<TResult> query, IRequestContext ctx, CancellationToken ct = default)
        {
            var res = await _exceptionMiddleware.ExecuteAsync(async () =>
            {
                var handlerType = typeof(IHandler<,>).MakeGenericType(query.GetType(), typeof(TResult));
                var handler = _registry.ServiceProvider.GetService(handlerType);

                if (handler is null) return Result<TResult>.Failure($"No handler registered for '{query.GetType().Name}'.");

                var method = handlerType.GetMethod("HandleAsync"); // Unambiguous
                var task = (Task<TResult>)method!.Invoke(handler, [query, ct])!;

                return Result<TResult>.Success(await task);
            }, query.GetType().Name, ctx.RequestorId);

            if (!res.IsSuccess) throw new Exception(res.Error);
            return res.Value!;
        }

        /// <summary>
        /// Publishes an Event to the BackendBus. (No-op in V1)
        /// </summary>
        /// <param name="event"></param>
        /// <param name="ctx"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public Task<Result> PublishAsync(IEvent @event, IRequestContext ctx, CancellationToken ct = default)
        {
            _ = @event; _ = ctx; _ = ct;
            return Task.FromResult(Result.Success());
        }

        /// <summary>
        /// Enqueues a Job to the BackendBus. (No-op in V1)
        /// </summary>
        /// <param name="job"></param>
        /// <param name="ctx"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public Task<string> EnqueueJobAsync(IJob job, IRequestContext ctx, CancellationToken ct = default)
        {
            _ = job; _ = ctx; _ = ct;
            return Task.FromResult(Guid.NewGuid().ToString());
        }

        /// <summary>
        /// Streams data from the BackendBus. (Not implemented in V1)
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="request"></param>
        /// <param name="ctx"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public IAsyncEnumerable<TResult> StreamAsync<TResult>(IStreamRequest<TResult> request, IRequestContext ctx, CancellationToken ct = default)
        {
            _ = request; _ = ctx; _ = ct;
            throw new NotImplementedException("Streaming not yet implemented in BackendBus V1");
        }

        // --- Explicit IPipeline Implementation (Fixes CS9334 & CS0535) ---
        // IPipeline expects Task (void) return for generic dispatch, not Task<Result>

        Task IPipeline.SendAsync(IMessage message, IRequestContext context, CancellationToken ct)
        {
            if (message is ICommand cmd) return SendAsync(cmd, context, ct);
            return Task.FromException(new InvalidOperationException("Message is not an ICommand"));
        }

        Task<TResult> IPipeline.SendAsync<TResult>(IMessage message, IRequestContext context, CancellationToken ct)
        {
            if (message is ICommand<TResult> cmd) return SendAsync(cmd, context, ct);
            throw new InvalidOperationException("Message is not an ICommand<T>");
        }

        Task<TResult> IPipeline.QueryAsync<TResult>(IMessage message, IRequestContext context, CancellationToken ct)
        {
            if (message is IQuery<TResult> q) return QueryAsync(q, context, ct);
            throw new InvalidOperationException("Message is not an IQuery<T>");
        }

        Task IPipeline.PublishAsync(IMessage message, IRequestContext context, CancellationToken ct) => Task.CompletedTask;

        Task<string> IPipeline.EnqueueJobAsync(IMessage message, IRequestContext context, CancellationToken ct) => Task.FromResult(Guid.NewGuid().ToString());

        IAsyncEnumerable<TResult> IPipeline.StreamAsync<TResult>(IMessage message, IRequestContext context, CancellationToken ct) => throw new NotImplementedException();

        // --- Helpers ---
        private static bool IsQuery(Type t) => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQuery<>));
        private static bool IsCommandWithResult(Type t) => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>));
        private static Type GetResultType(Type t) => t.GetInterfaces().FirstOrDefault(x => x.IsGenericType && (x.GetGenericTypeDefinition() == typeof(IQuery<>) || x.GetGenericTypeDefinition() == typeof(ICommand<>)))?.GetGenericArguments()[0] ?? typeof(object);
    }
}