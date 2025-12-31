namespace DataWarehouse.Contracts
{
    /// <summary>
    /// Represents a file found during a storage scan
    /// </summary>
    public record StorageListItem(Uri Uri, long SizeBytes);

    /// <summary>
    /// List all files
    /// </summary>
    public interface IListableStorage
    {
        /// <summary>
        /// Returns all Blob URIs currently stored
        /// </summary>
        /// <param name="prefix"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        IAsyncEnumerable<StorageListItem> ListFilesAsync(string prefix = "", CancellationToken ct = default);
    }
}