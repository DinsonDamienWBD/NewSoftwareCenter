using System;

namespace Core.Primitives
{
    /// <summary>
    /// Encapsulates sensitive data to prevent accidental logging.
    /// ToString() always returns "[REDACTED]".
    /// </summary>
    /// <typeparam name="T">The type of the secret (usually string or byte[]).</typeparam>
    public readonly struct Secret<T>(T value)
    {
        private readonly T _value = value;

        /// <summary>
        /// Explicitly reveals the secret value.
        /// Usage: var password = user.Password.Unveil();
        /// </summary>
        public T Unveil() => _value;

        /// <summary>
        /// Returns a safe string for logging.
        /// </summary>
        public override string ToString() => "[REDACTED]";

        /// <summary>
        /// Implicit conversions for ease of use (optional, depends on strictness)
        /// </summary>
        /// <param name="value"></param>
        public static implicit operator Secret<T>(T value) => new(value);
    }
}