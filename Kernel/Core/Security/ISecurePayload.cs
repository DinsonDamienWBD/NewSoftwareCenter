namespace Core.Security
{
    /// <summary>
    /// The "Fragile" Sticker.
    /// Implementing this interface on a Message signals the Middleware to 
    /// encrypt the payload transparently before it reaches storage.
    /// </summary>
    public interface ISecurePayload
    {
        /// <summary>
        /// Gets a value indicating whether the payload should be encrypted.
        /// </summary>
        bool EncryptPayload { get; }
    }
}