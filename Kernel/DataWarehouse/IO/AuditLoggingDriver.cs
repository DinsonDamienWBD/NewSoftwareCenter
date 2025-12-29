using Core.Data;
using Core.Primitives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace DataWarehouse.IO
{
    /// <summary>
    /// Driver that wraps another driver and logs everything
    /// </summary>
    public class AuditLoggingDriver(IStorageDriver inner, string logRoot, string contextName) : IStorageDriver
    {
        private readonly IStorageDriver _inner = inner;
        private readonly string _logPath = Path.Combine(logRoot, "audit_access.log");
        private readonly string _contextName = contextName; // e.g., "ModuleA" or "System"

        /// <summary>
        /// Storage capabilities of the inner driver
        /// </summary>
        public StorageCapabilities Capabilities => _inner.Capabilities;

        /// <summary>
        /// Log everything async
        /// </summary>
        /// <param name="action"></param>
        /// <param name="path"></param>
        /// <param name="details"></param>
        /// <returns></returns>
        private async Task LogAsync(string action, string path, string details = "")
        {
            // Format: Timestamp | Context | Action | Path | Details
            var line = $"{DateTime.UtcNow:O}|{_contextName}|{action}|{path}|{details}{Environment.NewLine}";

            // Simple append. In high-scale, use a proper logging queue.
            await File.AppendAllTextAsync(_logPath, line);
        }

        /// <summary>
        /// Log the write and call the inner save async
        /// </summary>
        /// <param name="path"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public async Task SaveAsync(string path, Stream data)
        {
            await LogAsync("WRITE", path, $"Size: {data.Length}");
            await _inner.SaveAsync(path, data);
        }

        /// <summary>
        /// Log the read and call inner load async
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public async Task<Stream> LoadAsync(string path)
        {
            await LogAsync("READ", path);
            return await _inner.LoadAsync(path);
        }

        /// <summary>
        /// Log the delete and call the inner delete async
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public async Task DeleteAsync(string path)
        {
            await LogAsync("DELETE", path);
            await _inner.DeleteAsync(path);
        }

        /// <summary>
        /// Call the inner Exists async
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public Task<bool> ExistsAsync(string path) => _inner.ExistsAsync(path);

        /// <summary>
        /// Call the inner Get details async
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public Task<FileDetails> GetDetailsAsync(string path) => _inner.GetDetailsAsync(path);

        /// <summary>
        /// Call the inner Get checksum async
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public Task<string> GetChecksumAsync(string path) => _inner.GetChecksumAsync(path);

        /// <summary>
        /// log the batch write and call the inner save batch async
        /// </summary>
        /// <param name="files"></param>
        /// <returns></returns>
        public async Task SaveBatchAsync(IDictionary<string, Stream> files)
        {
            await LogAsync("BATCH_WRITE", $"{files.Count} files");
            await _inner.SaveBatchAsync(files);
        }

        /// <summary>
        /// log the batch delete and call the inner delete batch async
        /// </summary>
        /// <param name="paths"></param>
        /// <returns></returns>
        public async Task DeleteBatchAsync(IEnumerable<string> paths)
        {
            await LogAsync("BATCH_DELETE", "Multiple files");
            await _inner.DeleteBatchAsync(paths);
        }
    }
}