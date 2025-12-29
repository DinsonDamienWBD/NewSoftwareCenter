using Core.Primitives;

namespace Core.Contracts
{
    /// <summary>
    /// Standard interface for Validation Rules.
    /// The Pipeline automatically finds and executes these before the Handler.
    /// </summary>
    public interface IValidator<in TMessage>
    {
        /// <summary>
        /// Validates the given instance asynchronously.
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        Task<IEnumerable<ValidationError>> ValidateAsync(TMessage instance, CancellationToken ct);
    }
}