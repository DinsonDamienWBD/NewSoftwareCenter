using Core.Data;
using Core.Primitives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace DataWarehouse.IO
{
    /// <summary>
    /// Physical implementation of IStorageDriver for Local Disk (NTFS/ReFS).
    /// </summary>
    public class LocalDiskDriver : IStorageDriver
    {
        private readonly string _rootPath;

        /// <summary>
        /// Storage capabilities
        /// </summary>
        public StorageCapabilities Capabilities =>
            StorageCapabilities.NativeAccessControl | StorageCapabilities.NativeBatching;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="rootPath"></param>
        public LocalDiskDriver(string rootPath)
        {
            if (!Directory.Exists(rootPath)) Directory.CreateDirectory(rootPath);
            _rootPath = rootPath;
        }

        /// <summary>
        /// Save data with atomic writes
        /// </summary>
        /// <param name="path"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public async Task SaveAsync(string path, Stream data)
        {
            var fullPath = PathSanitizer.Resolve(_rootPath, path);
            var dir = Path.GetDirectoryName(fullPath);
            if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            // Atomic Write Pattern: Write to .tmp, then Move
            var tempPath = fullPath + ".tmp";

            await using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
            {
                data.Position = 0;
                await data.CopyToAsync(fileStream);
            }

            // Atomic Swap
            if (File.Exists(fullPath)) File.Delete(fullPath);
            File.Move(tempPath, fullPath);
        }

        /// <summary>
        /// Load data
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        /// <exception cref="FileNotFoundException"></exception>
        public async Task<Stream> LoadAsync(string path)
        {
            var fullPath = PathSanitizer.Resolve(_rootPath, path);
            if (!File.Exists(fullPath)) throw new FileNotFoundException($"File not found: {path}");

            // Open with FileShare.Read to allow concurrent readers
            var memory = new MemoryStream();
            using (var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true))
            {
                await fileStream.CopyToAsync(memory);
            }
            memory.Position = 0;
            return memory;
        }

        /// <summary>
        /// Delete data
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public Task DeleteAsync(string path)
        {
            var fullPath = PathSanitizer.Resolve(_rootPath, path);
            if (File.Exists(fullPath)) File.Delete(fullPath);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Check if data exists
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public Task<bool> ExistsAsync(string path)
        {
            var fullPath = PathSanitizer.Resolve(_rootPath, path);
            return Task.FromResult(File.Exists(fullPath));
        }

        /// <summary>
        /// Get details about file
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        /// <exception cref="FileNotFoundException"></exception>
        public Task<FileDetails> GetDetailsAsync(string path)
        {
            var fullPath = PathSanitizer.Resolve(_rootPath, path);
            var info = new FileInfo(fullPath);

            if (!info.Exists) throw new FileNotFoundException(path);

            return Task.FromResult(new FileDetails
            {
                Name = info.Name,
                SizeBytes = info.Length,
                LastModified = info.LastWriteTimeUtc,
                ContentType = CoreContentTypes.Binary, // Default
                Hash = "" // Calculation is expensive, skip for basic details
            });
        }

        /// <summary>
        /// Save data as async in batch
        /// </summary>
        /// <param name="files"></param>
        /// <returns></returns>
        public async Task SaveBatchAsync(IDictionary<string, Stream> files)
        {
            foreach (var file in files)
            {
                await SaveAsync(file.Key, file.Value);
            }
        }

        /// <summary>
        /// Delete data async in batch
        /// </summary>
        /// <param name="paths"></param>
        /// <returns></returns>
        public Task DeleteBatchAsync(IEnumerable<string> paths)
        {
            foreach (var path in paths)
            {
                DeleteAsync(path);
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Get checksum of data
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public async Task<string> GetChecksumAsync(string path)
        {
            using var stream = await LoadAsync(path);
            using var sha = SHA256.Create();
            var hash = await sha.ComputeHashAsync(stream);
            return Convert.ToHexStringLower(hash);
        }
    }
}