using DataWarehouse.SDK.Contracts;

namespace DataWarehouse.Kernel.IO
{
    /// <summary>
    /// Local disk provider (Internal Utility - Not a Plugin).
    /// Lightweight local filesystem storage for internal kernel operations.
    /// For plugin-based local storage, use LocalStorageEngine instead.
    /// </summary>
    /// <param name="rootPath">Root directory for storage</param>
    public class LocalDiskProvider(string rootPath) : IStorageBackend
    {
        /// <summary>
        /// Scheme
        /// </summary>
        public string Scheme => "file";
        private readonly string _root = rootPath;

        /// <summary>
        /// Save
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public async Task SaveAsync(Uri uri, Stream data)
        {
            var path = GetPath(uri);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            await data.CopyToAsync(fs);
        }

        /// <summary>
        /// Load
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        /// <exception cref="FileNotFoundException"></exception>
        public Task<Stream> LoadAsync(Uri uri)
        {
            var path = GetPath(uri);
            if (!File.Exists(path)) throw new FileNotFoundException(path);
            return Task.FromResult<Stream>(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read));
        }

        /// <summary>
        /// Delete
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public Task DeleteAsync(Uri uri)
        {
            var path = GetPath(uri);
            if (File.Exists(path)) File.Delete(path);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Exists
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public Task<bool> ExistsAsync(Uri uri) => Task.FromResult(File.Exists(GetPath(uri)));

        private string GetPath(Uri uri)
        {
            // file:///container/blob -> root/container/blob
            string rel = uri.AbsolutePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(_root, rel);
        }
    }
}