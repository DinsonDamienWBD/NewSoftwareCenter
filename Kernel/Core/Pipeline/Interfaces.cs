using Core.Messages;

namespace Core.Pipeline
{
    /// <summary>
    /// Ordering protocol
    /// </summary>
    public enum MiddlewareStep
    {
        /// <summary>
        /// Initialization step
        /// </summary>
        Initialization = 0,

        /// <summary>
        /// Authentication step
        /// </summary>
        Authentication = 100,
        
        /// <summary>
        /// Rate limiting step
        /// </summary>
        RateLimiting = 200,

        /// <summary>
        /// Validation step
        /// </summary>
        Validation = 300,

        /// <summary>
        /// Execution step
        /// </summary>
        Execution = 1000,

        /// <summary>
        /// Post processing step
        /// </summary>
        PostProcessing = 2000
    }

    /// <summary>
    /// The primary dispatch highway.
    /// </summary>
    public interface IPipeline
    {
        /// <summary>Dispatches a Unicast Command/Query and awaits a response.</summary>
        Task<TResponse> DispatchAsync<TResponse>(IMessage message, CancellationToken ct = default)
            where TResponse : MessageBase;

        /// <summary>Publishes a Multicast Event (Fire-and-Forget).</summary>
        Task PublishAsync(IMessage message, CancellationToken ct = default);
    }

    /// <summary>
    /// OnErrorAsync Hook
    /// </summary>
    public interface IPipelineHook
    {
        /// <summary>
        /// On start async
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        Task OnStartAsync(PipelineContext context);

        /// <summary>
        /// On end async
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        Task OnEndAsync(PipelineContext context);

        /// <summary>
        /// On error async: Global Error Hook
        /// </summary>
        /// <param name="context"></param>
        /// <param name="ex"></param>
        /// <returns></returns>
        Task OnErrorAsync(PipelineContext context, Exception ex);
    }

    /// <summary>
    /// Contract for interceptors in the pipeline (Auth, Validation, Encryption).
    /// </summary>
    public interface IMiddleware
    {
        /// <summary>
        /// Intercepts the pipeline execution.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="next"></param>
        /// <returns></returns>
        Task InvokeAsync(PipelineContext context, Func<Task> next);

        /// <summary>
        /// Optional property, implementation can rely on registration order or explicit step 
        /// </summary>
        MiddlewareStep Step => MiddlewareStep.Execution;
    }

    /// <summary>
    /// The destination handler for a Command or Query.
    /// </summary>
    public interface IHandle<in TMessage> where TMessage : MessageBase
    {
        /// <summary>
        /// Handles the incoming message and produces a response.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        Task<MessageBase> Handle(TMessage message, CancellationToken ct);
    }

    /// <summary>
    /// The destination consumer for an Event (allows multiple consumers).
    /// </summary>
    public interface IConsume<in TEvent> where TEvent : Event
    {
        /// <summary>
        /// Consumes the incoming event.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        Task Consume(TEvent message, CancellationToken ct);
    }
}