namespace Core.Primitives
{
    /// <summary>
    /// Base class for all known application exceptions.
    /// The Pipeline catches these specifically to format standardized responses.
    /// </summary>
    public class SoftwareCenterException : Exception
    {
        /// <summary>
        /// Exception with a specific message.
        /// </summary>
        /// <param name="message"></param>
        public SoftwareCenterException(string message) : base(message) { }

        /// <summary>
        /// Exception with a specific message and inner exception.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="inner"></param>
        public SoftwareCenterException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Thrown when a security policy (ACL) is violated.
    /// Triggers a security audit log entry.
    /// </summary>
    /// <remarks>
    /// Security Exception with a specific message.
    /// </remarks>
    /// <param name="message"></param>
    public class SecurityException(string message) : SoftwareCenterException(message)
    {
    }

    /// <summary>
    /// Represents a single field error in a validation failure.
    /// </summary>
    /// <remarks>
    /// Validation error for a specific field.
    /// </remarks>
    /// <param name="field"></param>
    /// <param name="message"></param>
    /// <param name="severity"></param>
    public class ValidationError(string field, string message, ValidationSeverity severity = ValidationSeverity.Error)
    {
        /// <summary>
        /// Violated field name.
        /// </summary>
        public string Field { get; } = field;

        /// <summary>
        /// Violation message.
        /// </summary>
        public string Message { get; } = message;

        /// <summary>
        /// Get the severity of the validation
        /// </summary>
        public ValidationSeverity Severity { get; } = severity;
    }

    /// <summary>
    /// Thrown when input data fails business rules.
    /// Carries a structured list of errors for the UI to render.
    /// </summary>
    public class ValidationException : SoftwareCenterException
    {
        /// <summary>
        /// The collection of specific field errors.
        /// </summary>
        public IEnumerable<ValidationError> Errors { get; }

        /// <summary>
        /// Validation Exception with a general message.
        /// </summary>
        /// <param name="message"></param>
        public ValidationException(string message) : base(message)
        {
            Errors = [new ValidationError("General", message)];
        }


        /// <summary>
        /// Validation Exception with multiple field errors.
        /// </summary>
        /// <param name="errors"></param>
        public ValidationException(IEnumerable<ValidationError> errors)
            : base("Validation failed with multiple errors.")
        {
            Errors = errors;
        }
    }
}