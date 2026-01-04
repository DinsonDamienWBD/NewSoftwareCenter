using DataWarehouse.Plugins.Storage.LocalFileSystem.Engine;
using DataWarehouse.Plugins.Storage.LocalFileSystem.Services;
using DataWarehouse.SDK.Contracts;

namespace DataWarehouse.Plugins.Storage.LocalFileSystem.Bootstrapper
{
    public class LocalFileSystemStoragePlugin : IFeaturePlugin, IStorageProvider
    {
        public string Id => "filesystem-storage";
        public string Name => "Hybrid FileSystem Storage";
        public string Version => "3.0.0";
        public string Scheme => "file";

        private IStorageEngine? _activeEngine;

        public void Initialize(IKernelContext context)
        {
            // Check Config (In V5 we'd inject IConfiguration, here we infer or default)
            // Default: Folder Mode. 
            // To use VDI, one would set "StorageMode": "VDI" in appsettings.

            string root = Path.Combine(context.RootPath, "Data");
            Directory.CreateDirectory(root);

            bool useVdi = File.Exists(Path.Combine(root, "main.vdi")); // Auto-detect

            if (useVdi)
            {
                context.LogInfo($"[{Id}] Mode: VDI (Virtual Disk Container)");
                _activeEngine = new VirtualDiskEngine(root);
            }
            else
            {
                context.LogInfo($"[{Id}] Mode: Folder (OS Filesystem)");
                _activeEngine = new PhysicalFolderEngine(root);
            }
        }

        public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
        public Task StopAsync() => _activeEngine?.DisposeAsync() ?? Task.CompletedTask;

        // --- Proxy ---
        public Task SaveAsync(Uri uri, Stream data) => _activeEngine!.SaveAsync(uri, data);
        public Task<Stream> LoadAsync(Uri uri) => _activeEngine!.LoadAsync(uri);
        public Task DeleteAsync(Uri uri) => _activeEngine!.DeleteAsync(uri);
        public Task<bool> ExistsAsync(Uri uri) => _activeEngine!.ExistsAsync(uri);
    }
}