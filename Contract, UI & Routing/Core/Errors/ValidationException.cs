using System;

namespace SoftwareCenter.Core.Errors
{
    /// <summary>
    /// Represents an error that occurs during command validation.
    /// </summary>
    public class ValidationException : Exception
    {
        public ValidationException(string message) : base(message) { }
        public ValidationException(string message, Exception innerException) : base(message, innerException) { }
    }
}
