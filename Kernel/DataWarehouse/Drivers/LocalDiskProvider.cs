using DataWarehouse.Contracts;
using System.Runtime.CompilerServices;

namespace DataWarehouse.Drivers
{
    /// <summary>
    /// Local disk provider: the concrete driver
    /// </summary>
    public class LocalDiskProvider : IStorageProvider, IListableStorage
    {
        /// <summary>
        /// ID
        /// </summary>
        public string Id => "LocalDisk-NTFS";

        /// <summary>
        /// Version
        /// </summary>
        public string Version => "2.0";

        /// <summary>
        /// Data scheme
        /// Handles "file://"
        /// </summary>
        public string Scheme => "file"; 

        private readonly string _rootPath;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="rootPath"></param>
        public LocalDiskProvider(string rootPath)
        {
            _rootPath = rootPath;
            Directory.CreateDirectory(_rootPath);
        }

        private string GetPath(Uri uri)
        {
            // uri: file:///bucket/key.blob -> C:\Storage\bucket\key.blob
            var relative = uri.AbsolutePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(_rootPath, relative);
        }

        /// <summary>
        /// Save data
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public async Task SaveAsync(Uri uri, Stream data)
        {
            var path = GetPath(uri);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            // Atomic Write
            var temp = path + ".tmp";
            using (var fs = new FileStream(temp, FileMode.Create)) { await data.CopyToAsync(fs); }
            File.Move(temp, path, overwrite: true);
        }

        /// <summary>
        /// Load data
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        /// <exception cref="FileNotFoundException"></exception>
        public Task<Stream> LoadAsync(Uri uri)
        {
            var path = GetPath(uri);
            if (!File.Exists(path)) throw new FileNotFoundException(path);
            return Task.FromResult<Stream>(new FileStream(path, FileMode.Open, FileAccess.Read));
        }


        /// <summary>
        /// Delete data
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
        /// Check if data exists
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public Task<bool> ExistsAsync(Uri uri) => Task.FromResult(File.Exists(GetPath(uri)));

        /// <summary>
        /// List all files
        /// </summary>
        /// <param name="prefix"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public async IAsyncEnumerable<StorageListItem> ListFilesAsync(
            string prefix = "",
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            var dirInfo = new DirectoryInfo(_rootPath);
            if (!dirInfo.Exists) yield break;

            // EnumerateFiles is more efficient than GetFiles (Streaming vs Buffering)
            foreach (var file in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                if (ct.IsCancellationRequested) yield break;

                // Convert physical path back to URI scheme
                // C:\Root\Bucket\Key.blob -> Bucket/Key.blob
                var relative = Path.GetRelativePath(_rootPath, file.FullName)
                                   .Replace(Path.DirectorySeparatorChar, '/');

                // Prefix Filter Logic
                if (!string.IsNullOrEmpty(prefix) && !relative.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Construct full URI: file:///Bucket/Key.blob
                var uri = new Uri($"{Scheme}:///{relative}");

                yield return new StorageListItem(uri, file.Length);

                await Task.Yield(); // Ensure async context is respected
            }
        }
    }
}