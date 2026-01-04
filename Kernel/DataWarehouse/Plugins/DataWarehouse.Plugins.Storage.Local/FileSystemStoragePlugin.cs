using DataWarehouse.SDK.Contracts;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DataWarehouse.Plugins.Storage.LocalFileSystem
{
    public class FileSystemStoragePlugin : IFeaturePlugin, IStorageProvider
    {
        public string Id => "filesystem-storage";
        public string Name => "Standard File System Storage";
        public string Version => "1.0.0";
        public string Scheme => "file"; // Handles file:// URIs

        private string _rootPath;
        private IKernelContext _context;

        public void Initialize(IKernelContext context)
        {
            _context = context;
            _rootPath = Path.Combine(context.RootPath, "Data"); // Default location
            Directory.CreateDirectory(_rootPath);
            context.LogInfo($"[{Id}] Mounted at {_rootPath}");
        }

        public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
        public Task StopAsync() => Task.CompletedTask;

        // --- IStorageProvider Implementation ---

        public Task SaveAsync(Uri uri, Stream data)
        {
            var path = GetPhysicalPath(uri);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            // Standard File Write
            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            return data.CopyToAsync(fs);
        }

        public Task<Stream> LoadAsync(Uri uri)
        {
            var path = GetPhysicalPath(uri);
            if (!File.Exists(path)) throw new FileNotFoundException($"Blob not found: {uri}");

            // Return stream
            return Task.FromResult<Stream>(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read));
        }

        public Task DeleteAsync(Uri uri)
        {
            var path = GetPhysicalPath(uri);
            if (File.Exists(path)) File.Delete(path);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(Uri uri)
        {
            return Task.FromResult(File.Exists(GetPhysicalPath(uri)));
        }

        private string GetPhysicalPath(Uri uri)
        {
            // Maps file:///partition/folder/blob -> Root/partition/folder/blob
            // Removes the scheme and leading slash
            var relPath = uri.AbsolutePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(_rootPath, relPath);
        }
    }
}