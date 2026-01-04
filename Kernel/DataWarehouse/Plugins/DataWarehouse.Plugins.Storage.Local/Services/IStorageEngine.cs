namespace DataWarehouse.Plugins.Storage.LocalFileSystem.Services
{
    internal interface IStorageEngine
    {
        Task SaveAsync(Uri uri, Stream data);
        Task<Stream> LoadAsync(Uri uri);
        Task DeleteAsync(Uri uri);
        Task<bool> ExistsAsync(Uri uri);
        Task DisposeAsync();
    }
}