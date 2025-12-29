using Core.Backend.Messages;

namespace Host.Modules
{
    /// <summary>
    /// The base class for all messages in the System and Developer module.
    /// </summary>
    public abstract class MessageBase : ICommand
    {
        /// <summary>
        /// Message Identifier.
        /// </summary>
        public Guid MessageId { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Timestamp of when the message was created.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
