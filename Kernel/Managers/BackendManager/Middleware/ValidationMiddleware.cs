using Core;
using Core.Pipeline;
using Core.Validation;
using Core.ServiceRegistry;
using System.ComponentModel.DataAnnotations;

namespace Manager.Middleware
{
    /// <summary>
    /// Validation Middleware to validate incoming messages.
    /// </summary>
    public class ValidationMiddleware(Manager.Registry.ServiceRegistry registry)
    {
        private readonly Manager.Registry.ServiceRegistry _registry = registry;

        /// <summary>
        /// Validates the incoming message using Data Annotations (e.g. [Required], [MaxLength]).
        /// </summary>
        public Result Validate(IMessage message)
        {
            // 1. Data Annotations (Legacy/Simple)
            var context = new System.ComponentModel.DataAnnotations.ValidationContext(message);
            var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
            if (!System.ComponentModel.DataAnnotations.Validator.TryValidateObject(message, context, results, true))
            {
                var errors = string.Join(", ", results.Select(r => r.ErrorMessage));
                return Result.Failure(errors, "ValidationError");
            }

            // 2. Fluent Validation (Advanced)
            var validatorType = typeof(IValidator<>).MakeGenericType(message.GetType());
            var validator = _registry.ServiceProvider.GetService(validatorType);

            if (validator != null)
            {
                var method = validatorType.GetMethod("ValidateAsync");
                // Synchronous wait is necessary here as Middleware pipeline signature is often synchronous for Validation
                // Or we make the Middleware Async-First. For V1 we use .Result (safe if validator is pure logic)
                var task = (Task<Core.Validation.ValidationResult>)method!.Invoke(validator, [message, CancellationToken.None])!;
                var result = task.GetAwaiter().GetResult();

                if (!result.IsValid)
                {
                    return Result.Failure(string.Join("; ", result.Errors.Select(e => e.ErrorMessage)), "FluentValidation");
                }
            }

            return Result.Success();
        }
    }
}