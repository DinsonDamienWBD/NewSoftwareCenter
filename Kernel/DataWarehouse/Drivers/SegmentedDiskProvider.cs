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
        private readonly SemaphoreSlim _stateLock = new(1, 1);

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
            var files = Directory.GetFiles(_rootPath, "segment_*.dat");
            if (files.Length > 0)
            {
                Array.Sort(files); // Ensure we find the actual last one
                var lastFile = files.Last();
                string name = Path.GetFileNameWithoutExtension(lastFile);
                if (int.TryParse(name.Replace("segment_", ""), out int id))
                {
                    _currentSegmentId = id;
                    _currentOffset = new FileInfo(lastFile).Length;
                }
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
            int assignedSegment;
            long assignedOffset;
            long length = data.Length;

            // 1. FAST STATE RESERVATION (Optimistic)
            await _stateLock.WaitAsync();
            try
            {
                if (_currentOffset + length > SegmentSizeLimit)
                {
                    _currentSegmentId++;
                    _currentOffset = 0;
                }
                assignedSegment = _currentSegmentId;
                assignedOffset = _currentOffset;
                _currentOffset += length;
            }
            finally
            {
                _stateLock.Release();
            }

            // 2. SLOW IO (Parallel)
            string path = Path.Combine(_rootPath, GetSegmentName(assignedSegment));

            // FileShare.ReadWrite is CRITICAL here to allow concurrent access
            using var fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite);

            if (fs.Position != assignedOffset) fs.Seek(assignedOffset, SeekOrigin.Begin);

            await data.CopyToAsync(fs);

            // 3. FLUSH BARRIER (God Tier Reliability)
            // Forces the OS to empty the page cache to the physical disk controller.
            // Prevents data loss on power failure.
            fs.Flush(flushToDisk: true);
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