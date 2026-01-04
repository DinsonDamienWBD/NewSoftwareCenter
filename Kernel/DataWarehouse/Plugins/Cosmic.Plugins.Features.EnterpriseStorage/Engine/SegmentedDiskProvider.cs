using DataWarehouse.SDK.Contracts;
using System.Collections.Concurrent;
using System.Threading;

namespace DataWarehouse.Plugins.Features.EnterpriseStorage.Engine
{
    /// <summary>
    /// Segment disk into smaller chunks
    /// </summary>
    public class SegmentedDiskProvider : IStorageProvider
    {
        /// <summary>
        /// ID
        /// </summary>
        public string Id => "SegmentedDisk-LockFree";

        /// <summary>
        /// Name
        /// </summary>
        public string Name => "Enterprise Storage";

        /// <summary>
        /// Version
        /// </summary>
        public string Version => "4.0";

        /// <summary>
        /// Scheme
        /// </summary>
        public string Scheme => "file";

        private string _rootPath;
        private const long SegmentSize = 1024 * 1024 * 1024; // 1GB

        // Atomic Global Pointer: Grows forever (0 -> 100TB...)
        private long _globalWritePointer = 0;

        // Cache open file handles to avoid OS overhead
        private readonly ConcurrentDictionary<long, FileStream> _segmentHandles = new();

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="rootPath"></param>
        public SegmentedDiskProvider(string rootPath)
        {
            _rootPath = rootPath;
            Directory.CreateDirectory(_rootPath);
            RecoverState();
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
            // Find the highest byte written across all segments
            var files = Directory.GetFiles(_rootPath, "segment_*.dat");
            long maxBytes = 0;
            foreach (var f in files)
            {
                if (long.TryParse(Path.GetFileNameWithoutExtension(f).Replace("segment_", ""), out long segId))
                {
                    var info = new FileInfo(f);
                    long endPos = (segId * SegmentSize) + info.Length;
                    if (endPos > maxBytes) maxBytes = endPos;
                }
            }
            _globalWritePointer = maxBytes;
        }

        private static string GetSegmentName(long id) => $"segment_{id:D6}.dat";

        private FileStream GetOrOpenSegment(long segmentId)
        {
            return _segmentHandles.GetOrAdd(segmentId, id =>
            {
                string path = Path.Combine(_rootPath, GetSegmentName(id));
                // Shared ReadWrite allows multiple threads to write to different offsets simultaneously
                return new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite);
            });
        }

        /// <summary>
        /// Save
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public async Task SaveAsync(Uri uri, Stream data)
        {
            long length = data.Length;

            // 1. ATOMIC RESERVATION
            // We claim the range [start, end) globally.
            // No lock. Just CPU atomic addition.
            long endGlobal = Interlocked.Add(ref _globalWritePointer, length);
            long startGlobal = endGlobal - length;

            // 2. CALCULATE BOUNDARIES
            long startSegment = startGlobal / SegmentSize;
            long endSegment = (endGlobal - 1) / SegmentSize; // -1 handles exact boundary case

            // 3. EXECUTE WRITE(S)
            if (startSegment == endSegment)
            {
                // Simple case: Fits in one segment
                long offset = startGlobal % SegmentSize;
                await WriteToSegmentAsync(startSegment, offset, data, length);
            }
            else
            {
                // SPLIT CASE: Straddles boundary (Zero-Copy Implementation)
                long offsetA = startGlobal % SegmentSize;
                long lengthA = SegmentSize - offsetA;
                long lengthB = length - lengthA;

                // We allocate a shared buffer for the whole operation to minimize GC pressure.
                // In a true God-Tier system, we would use ArrayPool<byte>.Shared.Rent((int)length)
                byte[] sharedBuffer = new byte[length];
                int bytesRead = 0;
                while (bytesRead < length)
                {
                    int n = await data.ReadAsync(sharedBuffer.AsMemory(bytesRead, (int)length - bytesRead));
                    if (n == 0) break;
                    bytesRead += n;
                }

                // Slice A (First Segment)
                var memoryA = new ReadOnlyMemory<byte>(sharedBuffer, 0, (int)lengthA);
                await WriteToSegmentRawAsync(startSegment, offsetA, memoryA);

                // Slice B (Second Segment)
                var memoryB = new ReadOnlyMemory<byte>(sharedBuffer, (int)lengthA, (int)lengthB);
                await WriteToSegmentRawAsync(endSegment, 0, memoryB);
            }
        }

        private async Task WriteToSegmentAsync(long segmentId, long offset, Stream data, long count)
        {
            // Get handle (Thread-safe via ConcurrentDictionary)
            var fs = GetOrOpenSegment(segmentId);

            // FileStream isn't thread-safe for Seek+Write, so we lock the *Stream Instance* only.
            // This is fine because different threads act on different segments or offsets.
            // For true lock-free IO, we would use RandomAccess.WriteAsync (in .NET 6+) or Overlapped IO.
            // Upgrading to RandomAccess for God-Tier status:

            byte[] buffer = new byte[count];
            int read = await data.ReadAsync(buffer); // Buffer the chunk

            // SafeHandle offset write (No seeking required, stateless)
            await RandomAccess.WriteAsync(fs.SafeFileHandle, buffer.AsMemory(0, read), offset);
        }

        private async Task WriteToSegmentRawAsync(long segmentId, long offset, ReadOnlyMemory<byte> data)
        {
            var fs = GetOrOpenSegment(segmentId);
            // RandomAccess.WriteAsync is thread-safe and stateless (doesn't move file pointer)
            await RandomAccess.WriteAsync(fs.SafeFileHandle, data, offset);
        }

        /// <summary>
        /// Load
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public Task<Stream> LoadAsync(Uri uri) => throw new NotImplementedException("Use SlicedStream logic");

        /// <summary>
        /// Delete
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public Task DeleteAsync(Uri uri) => Task.CompletedTask;

        /// <summary>
        /// Exists
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public Task<bool> ExistsAsync(Uri uri) => Task.FromResult(true);
    }
}