using DataWarehouse.Plugins.Storage.LocalFileSystem.Services;
using DataWarehouse.SDK.Utilities; // For DurableState (Metadata)

namespace DataWarehouse.Plugins.Storage.LocalFileSystem.Engine
{
    /// <summary>
    /// VDI Implementation. Stores all blobs inside 'main.vdi'.
    /// Metadata (Offset/Length) is stored in 'vdi_index.json'.
    /// </summary>
    internal class VirtualDiskEngine : IStorageEngine
    {
        private readonly string _vdiPath;
        private readonly FileStream _vdiStream;
        private readonly DurableState<FileRecord> _index;
        private readonly SemaphoreSlim _ioLock = new(1, 1);
        private long _writeHead;

        public VirtualDiskEngine(string root)
        {
            _vdiPath = Path.Combine(root, "main.vdi");
            string indexPath = Path.Combine(root, "vdi_index.json");

            _index = new DurableState<FileRecord>(indexPath);

            // Open VDI in Random Access mode
            _vdiStream = new FileStream(_vdiPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
            _writeHead = _vdiStream.Length;
        }

        public async Task SaveAsync(Uri uri, Stream data)
        {
            long start = _writeHead;
            long length = data.Length;

            // Simple Append-Only logic (God Tier requires compaction, simplified here for MVP)
            await _ioLock.WaitAsync();
            try
            {
                _vdiStream.Position = _writeHead;
                await data.CopyToAsync(_vdiStream);
                _writeHead += length;
                await _vdiStream.FlushAsync();
            }
            finally
            {
                _ioLock.Release();
            }

            // Update Index
            _index.Set(uri.ToString(), new FileRecord { Offset = start, Length = length });
        }

        public async Task<Stream> LoadAsync(Uri uri)
        {
            if (!_index.TryGet(uri.ToString(), out var record))
                throw new FileNotFoundException("File not found in VDI");

            // Return a "Window" stream (SubStream)
            // Implementation requires a thread-safe read. 
            // For MVP, we copy to MemoryStream to avoid seeking conflicts on the shared _vdiStream.
            // Production VDI would use MemoryMappedFiles.

            var buffer = new byte[record.Length];
            await _ioLock.WaitAsync();
            try
            {
                _vdiStream.Position = record.Offset;
                await _vdiStream.ReadExactlyAsync(buffer.AsMemory(0, (int)record.Length));
            }
            finally { _ioLock.Release(); }

            return new MemoryStream(buffer);
        }

        public Task DeleteAsync(Uri uri)
        {
            // In Append-Only VDI, we just remove the index reference.
            // Compaction job (Garbage Collection) would reclaim space later.
            _index.Remove(uri.ToString());
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(Uri uri)
        {
            return Task.FromResult(_index.TryGet(uri.ToString(), out _));
        }

        public async Task DisposeAsync()
        {
            await _vdiStream.DisposeAsync();
            _index.Dispose();
            _ioLock.Dispose();
        }

        public class FileRecord
        {
            public long Offset { get; set; }
            public long Length { get; set; }
        }
    }
}