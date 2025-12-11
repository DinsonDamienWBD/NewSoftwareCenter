namespace SoftwareCenter.Core.Commands
{
    /// <summary>
    /// Marker interface for a command that does not return a value.
    /// A command is a request to perform an action and change the system state.
    /// </summary>
    public interface ICommand
    {
    }

    /// <summary>
    /// Marker interface for a command that returns a value of type <typeparamref name="TResult"/>.
    /// A command is a request to perform an action and change the system state.
    /// </summary>
    /// <typeparam name="TResult">The type of the result returned by the command handler.</typeparam>
    public interface ICommand<TResult> : ICommand
    {
    }
}
