namespace DataWarehouse.Contracts
{
    /// <summary>
    /// A storage event record
    /// </summary>
    /// <param name="Uri"></param>
    /// <param name="ETag"></param>
    /// <param name="Action"></param>
    /// <param name="Timestamp"></param>
    public record StorageEvent(string Uri, string ETag, string Action, long Timestamp);

    /// <summary>
    /// Real time provider
    /// </summary>
    public interface IRealTimeProvider : IPlugin
    {
        /// <summary>
        /// Publishes a change event to the global fabric.
        /// </summary>
        Task PublishAsync(StorageEvent evt);

        /// <summary>
        /// Subscribes to changes matching a URI pattern.
        /// </summary>
        Task<IAsyncDisposable> SubscribeAsync(string uriPattern, Action<StorageEvent> handler);
    }
}