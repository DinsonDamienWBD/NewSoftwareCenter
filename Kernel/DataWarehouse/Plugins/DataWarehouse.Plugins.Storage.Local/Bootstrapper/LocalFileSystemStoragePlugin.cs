using DataWarehouse.Plugins.Storage.LocalFileSystem.Configuration;
using DataWarehouse.Plugins.Storage.LocalFileSystem.Engine;
using DataWarehouse.Plugins.Storage.LocalFileSystem.Services;
using DataWarehouse.SDK.Contracts;
using Microsoft.Extensions.Configuration; // Requires Microsoft.Extensions.Configuration.Binder
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DataWarehouse.Plugins.Storage.LocalFileSystem.Bootstrapper
{
    /// <summary>
    /// The entry point for the Hybrid Storage Plugin.
    /// Selects between Physical Folder mode and Virtual Disk (VDI) mode based on configuration.
    /// </summary>
    [DataWarehouse.SDK.Attributes.PluginPriority(100, OperatingMode.Laptop)]
    public class LocalFileSystemStoragePlugin : IFeaturePlugin, IStorageProvider
    {
        /// <inheritdoc />
        public string Id => "filesystem-storage";

        /// <inheritdoc />
        public string Name => "Hybrid FileSystem Storage";

        /// <inheritdoc />
        public string Version => "5.0.0";

        /// <inheritdoc />
        public string Scheme => "file";

        private IStorageEngine? _activeEngine;
        private IKernelContext? _context;

        /// <summary>
        /// Handshake protocol handler
        /// </summary>
        public Task<HandshakeResponse> OnHandshakeAsync(HandshakeRequest request)
        {
            _context = request as IKernelContext;
            string dataRoot = Path.Combine(_context?.RootPath ?? "", "Data");
            Directory.CreateDirectory(dataRoot);

            var options = new LocalStorageOptions();

            if (File.Exists(Path.Combine(dataRoot, "main.vdi")))
            {
                options.Mode = StorageMode.Vdi;
            }

            if (_context?.Mode == OperatingMode.Hyperscale)
            {
                _context?.LogInfo($"[{Id}] Mode: PLATINUM (Sharded VDI)");
                _activeEngine = new ShardedStorageEngine(dataRoot, 16, options, _context!);
            }
            else if (options.Mode == StorageMode.Vdi)
            {
                _context?.LogInfo($"[{Id}] Initializing VDI Engine (Block-Based)...");
                _activeEngine = new VirtualDiskEngine(dataRoot, options, _context!);
            }
            else
            {
                _context?.LogInfo($"[{Id}] Initializing Physical Folder Engine...");
                _activeEngine = new PhysicalFolderEngine(dataRoot);
            }

            return Task.FromResult(HandshakeResponse.Success(
                pluginId: Id,
                name: Name,
                version: new Version(Version),
                category: PluginCategory.Storage
            ));
        }

        /// <summary>
        /// Message handler (optional).
        /// </summary>
        public Task OnMessageAsync(PluginMessage message)
        {
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public void Initialize(IKernelContext context)
        {
            _context = context;
            string dataRoot = Path.Combine(context.RootPath, "Data");
            Directory.CreateDirectory(dataRoot);

            // 1. Determine Mode (Logic: Config -> Auto-Detect -> Default)
            var options = new LocalStorageOptions();

            // In a real scenario, we bind from IConfiguration. Here we simulate/infer.
            // Check if VDI exists to auto-switch legacy installations
            if (File.Exists(Path.Combine(dataRoot, "main.vdi")))
            {
                options.Mode = StorageMode.Vdi;
            }

            // 2. Initialize Engine
            if (context.Mode == OperatingMode.Hyperscale)
            {
                context.LogInfo($"[{Id}] Mode: PLATINUM (Sharded VDI)");
                // 16 Shards for Hyperscale
                _activeEngine = new ShardedStorageEngine(dataRoot, 16, options, context);
            }
            else if (options.Mode == StorageMode.Vdi)
            {
                context.LogInfo($"[{Id}] Initializing VDI Engine (Block-Based)...");
                _activeEngine = new VirtualDiskEngine(dataRoot, options, context);
            }
            else
            {
                context.LogInfo($"[{Id}] Initializing Physical Folder Engine...");
                _activeEngine = new PhysicalFolderEngine(dataRoot);
            }
        }

        /// <inheritdoc />
        public Task StartAsync(CancellationToken ct) => Task.CompletedTask;

        /// <inheritdoc />
        public async Task StopAsync()
        {
            if (_activeEngine != null)
            {
                await _activeEngine.DisposeAsync();
                _context?.LogInfo($"[{Id}] Storage Engine Stopped.");
            }
        }

        // --- Proxy Implementation ---

        /// <inheritdoc />
        public Task SaveAsync(Uri uri, Stream data)
            => _activeEngine!.SaveAsync(uri, data);

        /// <inheritdoc />
        public Task<Stream> LoadAsync(Uri uri)
            => _activeEngine!.LoadAsync(uri);

        /// <inheritdoc />
        public Task DeleteAsync(Uri uri)
            => _activeEngine!.DeleteAsync(uri);

        /// <inheritdoc />
        public Task<bool> ExistsAsync(Uri uri)
            => _activeEngine!.ExistsAsync(uri);
    }
}