using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging; // Added for ILogger
using SoftwareCenter.Core.Commands;
using SoftwareCenter.Core.Diagnostics;

namespace SoftwareCenter.Kernel.Services
{
    /// <summary>
    /// Implements a smart command router that resolves and executes the highest priority
    /// command handler for a given command, with a fallback mechanism.
    /// </summary>
    public class SmartCommandRouter : ISmartCommandRouter
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IServiceRoutingRegistry _routingRegistry;
        private readonly ILogger<SmartCommandRouter> _logger; // Added ILogger

        /// <summary>
        /// Initializes a new instance of the <see cref="SmartCommandRouter"/> class.
        /// </summary>
        /// <param name="serviceProvider">The service provider to resolve handler instances.</param>
        /// <param name="routingRegistry">The routing registry to get handler priorities and types.</param>
        /// <param name="logger">The logger for diagnostics.</param>
        public SmartCommandRouter(IServiceProvider serviceProvider, IServiceRoutingRegistry routingRegistry, ILogger<SmartCommandRouter> logger)
        {
            _serviceProvider = serviceProvider;
            _routingRegistry = routingRegistry;
            _logger = logger;
        }

        public async Task Route(ICommand command, ITraceContext traceContext)
        {
            await ValidateCommand(command, traceContext);

            var commandType = command.GetType();
            var handlers = _routingRegistry.GetAllHandlers(commandType)
                                           .Where(r => r.HandlerInterfaceType.GetGenericTypeDefinition() == typeof(ICommandHandler<>));

            await RouteAndExecute(command, traceContext, handlers.ToList());
        }

        public async Task<TResult> Route<TResult>(ICommand<TResult> command, ITraceContext traceContext)
        {
            await ValidateCommand(command, traceContext);

            var commandType = command.GetType();
            var handlers = _routingRegistry.GetAllHandlers(commandType)
                                           .Where(r => r.HandlerInterfaceType.GetGenericTypeDefinition() == typeof(ICommandHandler<,>));
            
            return await RouteAndExecute<TResult>(command, traceContext, handlers.ToList());
        }

        private async Task ValidateCommand(ICommand command, ITraceContext traceContext)
        {
            var commandType = command.GetType();
            var validatorType = typeof(ICommandValidator<>).MakeGenericType(commandType);

            // Get all validators for this command type
            var validators = _serviceProvider.GetServices(validatorType);

            foreach (var validator in validators)
            {
                if (validator != null)
                {
                    try
                    {
                        await ((dynamic)validator).Validate((dynamic)command, traceContext);
                    }
                    catch (ValidationException vex)
                    {
                        _logger.LogWarning(vex, "Command validation failed for command '{CommandName}' (TraceId: {TraceId}).", commandType.Name, traceContext.TraceId);
                        throw; // Re-throw validation exceptions
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "An unexpected error occurred during validation of command '{CommandName}' (TraceId: {TraceId}).", commandType.Name, traceContext.TraceId);
                        throw;
                    }
                }
            }
        }

        private async Task RouteAndExecute(ICommand command, ITraceContext traceContext, List<HandlerRegistration> candidateHandlers)
        {
            foreach (var registration in candidateHandlers.OrderByDescending(h => h.Priority))
            {
                try
                {
                    var handler = _serviceProvider.GetService(registration.HandlerInterfaceType);
                    if (handler != null)
                    {
                        _logger.LogDebug("Routing command '{CommandName}' (TraceId: {TraceId}) to handler '{HandlerName}' (Priority: {Priority}, Module: {ModuleId}).",
                                         command.GetType().Name, traceContext.TraceId, registration.HandlerType.Name, registration.Priority, registration.OwningModuleId);
                        await (Task)((dynamic)handler).Handle((dynamic)command, traceContext);
                        return; // Command handled by highest priority, no need for fallback
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Handler '{HandlerName}' (Priority: {Priority}, Module: {ModuleId}) failed to process command '{CommandName}' (TraceId: {TraceId}). Attempting fallback if available. Error: {ErrorMessage}",
                                       registration.HandlerType.Name, registration.Priority, registration.OwningModuleId, command.GetType().Name, traceContext.TraceId, ex.Message);
                    // Continue to next handler for fallback
                }
            }

            _logger.LogError("No handler successfully processed command type '{CommandName}' (TraceId: {TraceId}).", command.GetType().Name, traceContext.TraceId);
            throw new InvalidOperationException($"No handler successfully processed command type '{command.GetType().Name}'.");
        }

        private async Task<TResult> RouteAndExecute<TResult>(ICommand<TResult> command, ITraceContext traceContext, List<HandlerRegistration> candidateHandlers)
        {
            foreach (var registration in candidateHandlers.OrderByDescending(h => h.Priority))
            {
                try
                {
                    var handler = _serviceProvider.GetService(registration.HandlerInterfaceType);
                    if (handler != null)
                    {
                        _logger.LogDebug("Routing command '{CommandName}' (TraceId: {TraceId}) to handler '{HandlerName}' (Priority: {Priority}, Module: {ModuleId}).",
                                         command.GetType().Name, traceContext.TraceId, registration.HandlerType.Name, registration.Priority, registration.OwningModuleId);
                        return await (Task<TResult>)((dynamic)handler).Handle((dynamic)command, traceContext);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Handler '{HandlerName}' (Priority: {Priority}, Module: {ModuleId}) failed to process command '{CommandName}' (TraceId: {TraceId}). Attempting fallback if available. Error: {ErrorMessage}",
                                       registration.HandlerType.Name, registration.Priority, registration.OwningModuleId, command.GetType().Name, traceContext.TraceId, ex.Message);
                    // Continue to next handler for fallback
                }
            }

            _logger.LogError("No handler successfully processed command type '{CommandName}' (TraceId: {TraceId}) and returned a result.", command.GetType().Name, traceContext.TraceId);
            throw new InvalidOperationException($"No handler successfully processed command type '{command.GetType().Name}' and returned a result.");
        }
    }
}
