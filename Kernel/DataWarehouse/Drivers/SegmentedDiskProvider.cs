using DataWarehouse.Contracts;

namespace DataWarehouse.Drivers
{
    /// <summary>
    /// Replaces LocalDiskProvider. Automatically packs data into 1GB chunks.
    /// </summary>
    public class SegmentedDiskProvider : IStorageProvider
    {
        /// <summary>
        /// ID
        /// </summary>
        public string Id => "SegmentedDisk";

        /// <summary>
        /// Version
        /// </summary>
        public string Version => "3.0";

        /// <summary>
        /// Scheme
        /// </summary>
        public string Scheme => "file";

        private readonly string _rootPath;
        private const long SegmentSizeLimit = 1024 * 1024 * 1024; // 1GB Segments
        private readonly SemaphoreSlim _writeLock = new(1, 1);

        // State
        private int _currentSegmentId = 0;
        private long _currentOffset = 0;

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

        private void RecoverState()
        {
            // Scan for the last segment
            var files = Directory.GetFiles(_rootPath, "segment_*.dat");
            if (files.Length > 0)
            {
                _currentSegmentId = files.Length - 1;
                var info = new FileInfo(Path.Combine(_rootPath, GetSegmentName(_currentSegmentId)));
                _currentOffset = info.Length;
            }
        }

        private static string GetSegmentName(int id) => $"segment_{id:D6}.dat";

        /// <summary>
        /// Save data
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public async Task SaveAsync(Uri uri, Stream data)
        {
            // URI format: file:///pool/{virtual_id}
            // We ignore the virtual_id for physical placement; we pack it.
            // But we must RETURN the physical location so the Manifest knows where to look.

            // NOTE: The Manifest logic needs to be updated to store "PhysicalURI" separate from "LogicalURI".
            // For this implementation, we assume the caller manages the mapping or we return a composite key.

            await _writeLock.WaitAsync();
            try
            {
                // 1. Check if we need a new segment
                if (_currentOffset + data.Length > SegmentSizeLimit)
                {
                    _currentSegmentId++;
                    _currentOffset = 0;
                }

                string segmentPath = Path.Combine(_rootPath, GetSegmentName(_currentSegmentId));

                // 2. Append Data (Efficient Sequential Write)
                using var fs = new FileStream(segmentPath, FileMode.Append, FileAccess.Write, FileShare.Read);
                // Record start position BEFORE writing
                long startPos = fs.Position;
                await data.CopyToAsync(fs);
                _currentOffset = fs.Position;

                // CRITICAL: We need to tell the Index where we put it!
                // We can't change the void return type of IStorageProvider.SaveAsync.
                // Solution: The 'uri' passed in acts as the KEY.
                // We must update the "Manifest" with: Key -> {SegmentID, Offset, Length}
                // This implies the 'SegmentedDiskProvider' is a lower-level primitive.
            }
            finally
            {
                _writeLock.Release();
            }
        }

        /// <summary>
        /// Load data
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public async Task<Stream> LoadAsync(Uri uri)
        {
            // The URI must contain the physical coordinates.
            // Format: file:///pool/segment_000001.dat?offset=1024&length=5000

            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);

            // Null Safety Checks
            string? offsetStr = query["offset"];
            string? lengthStr = query["length"];

            if (string.IsNullOrEmpty(offsetStr) || string.IsNullOrEmpty(lengthStr))
                throw new ArgumentException($"Invalid URI: Missing offset/length. {uri}");

            long offset = long.Parse(offsetStr);
            long length = long.Parse(lengthStr);

            string filename = Path.GetFileName(uri.AbsolutePath);
            string path = Path.Combine(_rootPath, filename);

            var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            fs.Seek(offset, SeekOrigin.Begin);
            return new SlicedStream(fs, length);
        }

        /// <summary>
        /// Delete data
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        public Task DeleteAsync(Uri uri)
        {
            // Segmented storage is append-only/WORM-like by default.
            // True deletion would require compacting the 1GB segment file.
            throw new NotSupportedException("Segmented Disk Provider does not support direct deletion.");
        }

        /// <summary>
        /// Check if data exists
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public Task<bool> ExistsAsync(Uri uri)
        {
            // Simple check: does the segment file exist?
            try
            {
                string filename = Path.GetFileName(uri.AbsolutePath);
                string path = Path.Combine(_rootPath, filename);
                return Task.FromResult(File.Exists(path));
            }
            catch { return Task.FromResult(false); }
        }
    }
}