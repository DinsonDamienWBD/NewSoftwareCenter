namespace Core.Log
{
    public interface IAuditLogger
    {
        Task LogExecutionAsync(string commandName, string user, string details);
    }
}