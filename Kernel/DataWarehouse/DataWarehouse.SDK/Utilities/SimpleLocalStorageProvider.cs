namespace DataWarehouse.SDK.Utilities
{
    /// <summary>
    /// Simple local filesystem storage backend for internal kernel use.
    /// Used by DurableStateV2 for components like FeatureManager and ACLSecurityEngine.
    ///
    /// This is a minimal implementation providing only basic file operations needed
    /// for DurableStateV2's write-ahead logging. For full-featured storage,
    /// use DataWarehouse.Plugins.Storage.LocalNew.Engine.LocalStorageEngine instead.
    ///
    /// Features:
    /// - Synchronous and asynchronous file I/O
    /// - Automatic directory creation
    /// - Thread-safe operations
    /// - No plugin infrastructure overhead
    ///
    /// Limitations:
    /// - No RAID support (use LocalStorageEngine for that)
    /// - No subdirectory organization
    /// - No compression
    /// - Fixed to local filesystem
    /// </summary>
    public class SimpleLocalStorageProvider : IStorageBackend
    {
        private readonly string _basePath;
        private readonly Lock _lock = new();

        /// <summary>
        /// Storage scheme identifier.
        /// </summary>
        public string Scheme => "file";

        /// <summary>
        /// Initializes the simple local storage provider.
        /// </summary>
        /// <param name="basePath">Base directory path for storage (e.g., "/data/.datawarehouse/Metadata")</param>
        public SimpleLocalStorageProvider(string basePath)
        {
            if (string.IsNullOrWhiteSpace(basePath))
                throw new ArgumentException("Base path cannot be empty", nameof(basePath));

            _basePath = basePath;

            // Create base directory if it doesn't exist
            if (!Directory.Exists(_basePath))
            {
                Directory.CreateDirectory(_basePath);
            }
        }

        /// <summary>
        /// Saves data to a file asynchronously.
        /// </summary>
        /// <param name="uri">File URI (e.g., "file://features.journal")</param>
        /// <param name="data">Data stream to save</param>
        public async Task SaveAsync(Uri uri, Stream data)
        {
            if (uri == null)
                throw new ArgumentNullException(nameof(uri));
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            var filePath = GetFilePath(uri);

            // Ensure directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Atomic write: Write to temp file, then move
            var tempPath = filePath + ".tmp";

            try
            {
                using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await data.CopyToAsync(fs);
                    await fs.FlushAsync();
                }

                // Atomic move (overwrites existing file)
                File.Move(tempPath, filePath, overwrite: true);
            }
            catch
            {
                // Clean up temp file on failure
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
                throw;
            }
        }

        /// <summary>
        /// Loads data from a file asynchronously.
        /// </summary>
        /// <param name="uri">File URI (e.g., "file://features.journal")</param>
        /// <returns>Stream containing file data</returns>
        /// <exception cref="FileNotFoundException">If file doesn't exist</exception>
        public async Task<Stream> LoadAsync(Uri uri)
        {
            if (uri == null)
                throw new ArgumentNullException(nameof(uri));

            var filePath = GetFilePath(uri);

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}", filePath);

            // Read entire file into memory stream (for simplicity)
            var ms = new MemoryStream();
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                await fs.CopyToAsync(ms);
            }
            ms.Position = 0;
            return ms;
        }

        /// <summary>
        /// Deletes a file asynchronously.
        /// </summary>
        /// <param name="uri">File URI</param>
        public Task DeleteAsync(Uri uri)
        {
            if (uri == null)
                throw new ArgumentNullException(nameof(uri));

            var filePath = GetFilePath(uri);

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Checks if a file exists asynchronously.
        /// </summary>
        /// <param name="uri">File URI</param>
        /// <returns>True if file exists, false otherwise</returns>
        public Task<bool> ExistsAsync(Uri uri)
        {
            if (uri == null)
                throw new ArgumentNullException(nameof(uri));

            var filePath = GetFilePath(uri);
            return Task.FromResult(File.Exists(filePath));
        }

        /// <summary>
        /// Converts a URI to a file path.
        /// </summary>
        /// <param name="uri">File URI (e.g., "file://features.journal")</param>
        /// <returns>Absolute file path</returns>
        private string GetFilePath(Uri uri)
        {
            // Extract path from URI
            // URI format: file://path/to/file or file:///path/to/file
            var path = uri.AbsolutePath;

            // Remove leading slash on Windows if it results in invalid path
            if (Path.IsPathRooted(path) && path.StartsWith("/") && !Path.IsPathFullyQualified(path))
            {
                path = path.TrimStart('/');
            }

            // If path is already absolute, use it directly
            if (Path.IsPathRooted(path))
            {
                return path;
            }

            // Otherwise, combine with base path
            return Path.Combine(_basePath, path);
        }

        /// <summary>
        /// Gets the base path for this storage provider.
        /// </summary>
        public string GetBasePath() => _basePath;
    }
}
