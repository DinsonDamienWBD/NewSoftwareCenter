using DataWarehouse.Plugins.Storage.LocalFileSystem.Services;

namespace DataWarehouse.Plugins.Storage.LocalFileSystem.Engine
{
    internal class PhysicalFolderEngine : IStorageEngine
    {
        private readonly string _root;

        public PhysicalFolderEngine(string root)
        {
            _root = root;
        }

        public async Task SaveAsync(Uri uri, Stream data)
        {
            var path = GetPath(uri);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            await data.CopyToAsync(fs);
        }

        public Task<Stream> LoadAsync(Uri uri)
        {
            var path = GetPath(uri);
            if (!File.Exists(path)) throw new FileNotFoundException(path);
            return Task.FromResult<Stream>(new FileStream(path, FileMode.Open, FileAccess.Read));
        }

        public Task DeleteAsync(Uri uri)
        {
            var path = GetPath(uri);
            if (File.Exists(path)) File.Delete(path);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(Uri uri) => Task.FromResult(File.Exists(GetPath(uri)));
        public Task DisposeAsync() => Task.CompletedTask;

        private string GetPath(Uri uri)
        {
            var rel = uri.AbsolutePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(_root, rel);
        }
    }
}