using Core.Data;
using Core.Primitives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace DataWarehouse.IO
{
    /// <summary>
    /// Driver that wraps another driver and logs everything
    /// </summary>
    public class AuditLoggingDriver : IStorageDriver, IDisposable, IAsyncDisposable
    {
        private readonly IStorageDriver _inner;
        private readonly string _logRoot;
        private readonly string _contextName; // e.g., "ModuleA" or "System"
        private const long MaxLogSizeBytes = 10 * 1024 * 1024; // 10MB

        // Async Batching
        private readonly Channel<string> _logChannel;
        private readonly Task _workerTask;
        private readonly CancellationTokenSource _cts;

        /// <summary>
        /// Storage capabilities of the inner driver
        /// </summary>
        public StorageCapabilities Capabilities => _inner.Capabilities;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="inner"></param>
        /// <param name="logRoot"></param>
        /// <param name="contextName"></param>
        public AuditLoggingDriver(IStorageDriver inner, string logRoot, string contextName)
        {
            _inner = inner;
            _logRoot = logRoot;
            _contextName = contextName;

            _logChannel = Channel.CreateUnbounded<string>();
            _cts = new CancellationTokenSource();
            _workerTask = Task.Run(ProcessLogsAsync);
        }

        private void EnqueueLog(string action, string path, string details = "")
        {
            var line = $"{DateTime.UtcNow:O}|{_contextName}|{action}|{path}|{details}{Environment.NewLine}";
            _logChannel.Writer.TryWrite(line);
        }

        private async Task ProcessLogsAsync()
        {
            var batch = new List<string>();
            var logFile = Path.Combine(_logRoot, "audit_access.log");

            try
            {
                while (await _logChannel.Reader.WaitToReadAsync(_cts.Token))
                {
                    // Read all available items
                    while (_logChannel.Reader.TryRead(out var item))
                    {
                        batch.Add(item);
                        if (batch.Count >= 50) break; // Batch limit
                    }

                    if (batch.Count > 0)
                    {
                        await CheckRotationAsync(logFile);
                        await File.AppendAllLinesAsync(logFile, batch, _cts.Token);
                        batch.Clear();
                    }
                }
            }
            catch (OperationCanceledException) { /* Graceful shutdown */ }
        }

        private Task CheckRotationAsync(string logFile)
        {
            if (File.Exists(logFile) && new FileInfo(logFile).Length > MaxLogSizeBytes)
            {
                var archiveName = $"audit_{DateTime.UtcNow:yyyy-MM-dd_HHmmss}.log";
                try
                {
                    File.Move(logFile, Path.Combine(_logRoot, archiveName));
                }
                catch { /* Ignore */ }
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Log the write and call inner Save async
        /// </summary>
        /// <param name="path"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public async Task SaveAsync(string path, Stream data)
        {
            EnqueueLog("WRITE", path, $"Size: {data.Length}");
            await _inner.SaveAsync(path, data);
        }

        /// <summary>
        /// Log the read and call inner read async
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public async Task<Stream> LoadAsync(string path)
        {
            EnqueueLog("READ", path);
            return await _inner.LoadAsync(path);
        }

        /// <summary>
        /// Log the delete and call the inner delete async
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public async Task DeleteAsync(string path)
        {
            EnqueueLog("DELETE", path);
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
            EnqueueLog("BATCH_WRITE", $"{files.Count} files");
            await _inner.SaveBatchAsync(files);
        }

        /// <summary>
        /// log the batch delete and call the inner delete batch async
        /// </summary>
        /// <param name="paths"></param>
        /// <returns></returns>
        public async Task DeleteBatchAsync(IEnumerable<string> paths)
        {
            EnqueueLog("BATCH_DELETE", "Multiple files");
            await _inner.DeleteBatchAsync(paths);
        }

        /// <summary>
        /// Safe disposal
        /// </summary>
        public void Dispose()
        {
            _cts.Cancel();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Safe disposal
        /// </summary>
        /// <returns></returns>
        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            try { await _workerTask; } catch { }
            GC.SuppressFinalize(this);
        }
    }
}