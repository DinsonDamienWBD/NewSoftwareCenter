using DataWarehouse.SDK.Contracts;
using DataWarehouse.SDK.Utilities;
using System.Collections.Concurrent;
using System.Threading;

namespace DataWarehouse.Plugins.Features.EnterpriseStorage.Engine
{
    /// <summary>
    /// PRODUCTION GRADE: Log-Structured Segmented Disk.
    /// Writes files sequentially into 1GB "Segments" to maximize write throughput (HDD friendly).
    /// Uses an internal index to map URIs to Segment/Offset/Length.
    /// </summary>
    public class SegmentedDiskProvider : IStorageProvider, IDisposable
    {
        /// <summary>
        /// ID
        /// </summary>
        public string Id => "SegmentedDisk-LogStruct";

        /// <summary>
        /// Name
        /// </summary>
        public string Name => "Enterprise Segmented Storage";

        /// <summary>
        /// Version
        /// </summary>
        public string Version => "5.0.0";

        /// <summary>
        /// Scheme
        /// </summary>
        public string Scheme => "seg";

        private string _rootPath;
        private const long SegmentSize = 1024 * 1024 * 1024; // 1GB

        // Index: Maps "seg://file1" -> { SegmentId: 0, Offset: 1024, Length: 500 }
        private readonly DurableState<SegmentInfo> _index;

        // Cache handles
        private readonly ConcurrentDictionary<long, FileStream> _segmentHandles = new();
        private readonly Lock _writeLock = new();

        private long _currentSegmentId = 0;
        private long _currentSegmentOffset = 0;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="rootPath"></param>
        public SegmentedDiskProvider(string rootPath)
        {
            _rootPath = rootPath;
            Directory.CreateDirectory(_rootPath);

            // Initialize Index
            _index = new DurableState<SegmentInfo>(Path.Combine(_rootPath, "segment_index.json"));

            // Recover Write Pointer
            RecoverState();
        }

        /// <summary>
        /// Handshake protocol handler
        /// </summary>
        public Task<HandshakeResponse> OnHandshakeAsync(HandshakeRequest request)
        {
            var context = request as IKernelContext;
            _rootPath = Path.Combine(context?.RootPath ?? "", "SegmentedData");
            Directory.CreateDirectory(_rootPath);

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

        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="context"></param>
        public void Initialize(IKernelContext context)
        {
            _rootPath = Path.Combine(context.RootPath, "SegmentedData");
            Directory.CreateDirectory(_rootPath);
        }

        private void RecoverState()
        {
            // Find the last used segment based on the highest SegmentID in the index
            // Simple approach: Start from 0, check file existence
            while (File.Exists(GetSegmentPath(_currentSegmentId + 1)))
            {
                _currentSegmentId++;
            }

            var info = new FileInfo(GetSegmentPath(_currentSegmentId));
            _currentSegmentOffset = info.Exists ? info.Length : 0;
        }

        private static string GetSegmentName(long id) => $"segment_{id:D6}.dat";

        /// <summary>
        /// Save
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public async Task SaveAsync(Uri uri, Stream data)
        {
            // 1. Determine Write Location
            long segId;
            long offset;
            long length = data.Length;

            // Log-Structured Write: Always append to end
            lock (_writeLock)
            {
                if (_currentSegmentOffset + length > SegmentSize)
                {
                    _currentSegmentId++;
                    _currentSegmentOffset = 0;
                }

                segId = _currentSegmentId;
                offset = _currentSegmentOffset;

                // Advance pointer immediately (optimistic reservation)
                _currentSegmentOffset += length;
            }

            // 2. Write Data
            var fs = GetOrOpenSegment(segId);

            // Use RandomAccess for thread-safety without moving the FileStream position
            byte[] buffer = new byte[81920]; // 80KB buffer
            long written = 0;
            int read;

            // Note: If stream is not seekable, we read linearly.
            if (data.CanSeek) data.Position = 0;

            while ((read = await data.ReadAsync(buffer)) > 0)
            {
                await RandomAccess.WriteAsync(fs.SafeFileHandle, buffer.AsMemory(0, read), offset + written);
                written += read;
            }

            // 3. Update Index
            _index.Set(uri.ToString(), new SegmentInfo
            {
                SegmentId = segId,
                Offset = offset,
                Length = length
            });

            // Flush periodically or rely on OS cache
            fs.Flush(); // Optional: Impact performance
        }

        /// <summary>
        /// Load
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        /// <exception cref="FileNotFoundException"></exception>
        public Task<Stream> LoadAsync(Uri uri)
        {
            // 1. Lookup Metadata
            if (!_index.TryGet(uri.ToString(), out var info) || info == null)
            {
                throw new FileNotFoundException($"Blob not found in segmented storage: {uri}");
            }

            // 2. Get Handle
            var fs = GetOrOpenSegment(info.SegmentId);

            // 3. Create Sliced Stream
            // IMPORTANT: SlicedStream expects the base stream.
            // Since we share FileStreams in _segmentHandles, we cannot change their Position safely.
            // SlicedStream must handle the offset internally relative to the base stream provided,
            // OR we must open a NEW unique handle for reading to ensure thread safety if SlicedStream uses Seek().

            // Strategy: For God-Tier Read Concurrency, we open a NEW Read-Only Shared stream for this request.
            // Sharing the Write handle for Reads will cause contention.

            var readStream = new FileStream(
                GetSegmentPath(info.SegmentId),
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                4096,
                FileOptions.Asynchronous);

            // Position the stream to the start of the slice
            readStream.Seek(info.Offset, SeekOrigin.Begin);

            // Return the Slice
            return Task.FromResult<Stream>(new SlicedStream(readStream, info.Length));
        }

        public Task DeleteAsync(Uri uri)
        {
            // Log-Structured File Systems typically don't delete immediately.
            // We just remove the index entry. Space is reclaimed via Garbage Collection (Vacuum) later.
            _index.Remove(uri.ToString(), out _);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(Uri uri)
        {
            return Task.FromResult(_index.TryGet(uri.ToString(), out _));
        }

        private FileStream GetOrOpenSegment(long id)
        {
            return _segmentHandles.GetOrAdd(id, _ =>
            {
                string path = GetSegmentPath(id);
                // Opened for Writing (Shared Read)
                return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, 4096, FileOptions.Asynchronous);
            });
        }

        private string GetSegmentPath(long id) => Path.Combine(_rootPath, $"segment_{id:000000}.dat");

        public void Dispose()
        {
            foreach (var fs in _segmentHandles.Values) fs.Dispose();
            _index.Dispose();
            GC.SuppressFinalize(this);
        }

        // --- Metadata DTO ---
        public class SegmentInfo
        {
            public long SegmentId { get; set; }
            public long Offset { get; set; }
            public long Length { get; set; }
        }
    }
}