using Core;
using Core.Validation; // Reuse Core contract
using Core.Pipeline;
using System.ComponentModel.DataAnnotations;

namespace FrontendManager.Middleware
{
    /// <summary>
    /// Validation Middleware for UI Bus messages
    /// </summary>
    /// <param name="registry"></param>
    public class UiValidationMiddleware(FrontendManager.Registry.UiRegistry registry)
    {
        private readonly FrontendManager.Registry.UiRegistry _registry = registry;

        /// <summary>
        /// Validate the message using Data Annotations and Fluent Validation
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public Result Validate(IMessage message)
        {
            // 1. Data Annotations (Basic Guard Clauses)
            var context = new ValidationContext(message);
            var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();

            if (!Validator.TryValidateObject(message, context, results, true))
            {
                var errors = string.Join(", ", results.Select(r => r.ErrorMessage));
                return Result.Failure(errors, "UiValidationError");
            }

            // 2. Fluent Validation (Complex Rules)
            // We use the same IValidator interface from Core
            var validatorType = typeof(IValidator<>).MakeGenericType(message.GetType());
            var validator = _registry.ServiceProvider.GetService(validatorType);

            if (validator != null)
            {
                var method = validatorType.GetMethod("ValidateAsync");
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