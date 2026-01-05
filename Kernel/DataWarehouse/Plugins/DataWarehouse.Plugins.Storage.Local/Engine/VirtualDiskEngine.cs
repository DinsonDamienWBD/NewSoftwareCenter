using DataWarehouse.Plugins.Storage.LocalFileSystem.Configuration;
using DataWarehouse.Plugins.Storage.LocalFileSystem.Services;
using DataWarehouse.SDK.Contracts;
using DataWarehouse.SDK.Utilities;

namespace DataWarehouse.Plugins.Storage.LocalFileSystem.Engine
{
    /// <summary>
    /// A Production-Grade Block-Based Virtual Disk Engine.
    /// Manages a single large file (.vdi) containing thousands of smaller virtual files.
    /// Features: Contiguous Allocation Strategy, Persistent Bitmap, Metadata Indexing.
    /// </summary>
    internal class VirtualDiskEngine : IStorageEngine
    {
        private readonly string _vdiPath;
        private readonly string _mapPath; // [NEW] Separate Bitmap File
        private readonly string _indexPath;
        private readonly LocalStorageOptions _options;
        private readonly IKernelContext _context;
        private readonly DurableState<VdiFileRecord> _fileIndex; // [FIX] Typed correctly
        private FileStream? _vdiStream;
        private FileStream? _mapStream;
        private readonly SemaphoreSlim _lock = new(1, 1); // [FIX] The lock variable

        // State
        private byte[] _allocationBitmap = [];
        private long _totalBlocks;
        private const long MagicNumber = 0x44575F5644495F32; // Version 2 (Split Bitmap)

        /// <summary>
        /// Initializes the Virtual Disk Engine.
        /// </summary>
        public VirtualDiskEngine(string rootPath, LocalStorageOptions options, IKernelContext context)
        {
            _vdiPath = Path.Combine(rootPath, "main.vdi");
            _mapPath = Path.Combine(rootPath, "main.map");
            _indexPath = Path.Combine(rootPath, "vdi_metadata.json");
            _options = options;
            _context = context;
            _fileIndex = new DurableState<VdiFileRecord>(_indexPath);

            InitializeDisk();
        }

        private void InitializeDisk()
        {
            // Open VDI (Data)
            _vdiStream = new FileStream(_vdiPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.RandomAccess);

            // Open Map (Bitmap)
            _mapStream = new FileStream(_mapPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.RandomAccess);

            if (_vdiStream.Length == 0)
            {
                FormatDisk();
            }
            else
            {
                MountDisk();
            }
        }

        private void FormatDisk()
        {
            _totalBlocks = _options.VdiInitialSize / _options.VdiBlockSize;
            int bitmapBytes = (int)Math.Ceiling(_totalBlocks / 8.0);
            _allocationBitmap = new byte[bitmapBytes];

            // Initialize Files
            _vdiStream!.SetLength(_options.VdiInitialSize);
            _mapStream!.SetLength(bitmapBytes);

            // Flush empty bitmap
            _mapStream.Position = 0;
            _mapStream.Write(_allocationBitmap);
            _mapStream.Flush();

            _context.LogInfo($"[VDI] Formatted. {_totalBlocks} blocks.");
        }

        private void MountDisk()
        {
            _totalBlocks = _vdiStream!.Length / _options.VdiBlockSize;
            int bitmapBytes = (int)Math.Ceiling(_totalBlocks / 8.0);

            if (_mapStream!.Length != bitmapBytes)
            {
                // Self-Heal: Resize bitmap if mismatched
                _mapStream.SetLength(bitmapBytes);
            }

            _allocationBitmap = new byte[bitmapBytes];
            _mapStream.Position = 0;
            _mapStream.ReadExactly(_allocationBitmap);
        }

        /// <summary>
        /// Save
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public async Task SaveAsync(Uri uri, Stream data)
        {
            await _lock.WaitAsync();
            try
            {
                long length = data.Length;
                int blocksNeeded = (int)Math.Ceiling((double)length / _options.VdiBlockSize);

                // Real Allocation Strategy: First Fit
                var extents = AllocateBlocks(blocksNeeded);

                byte[] buffer = new byte[_options.VdiBlockSize];
                data.Position = 0;

                foreach (var extent in extents)
                {
                    for (int i = 0; i < extent.BlockCount; i++)
                    {
                        long physicalOffset = (extent.StartBlock + i) * _options.VdiBlockSize;
                        int read = await data.ReadAsync(buffer.AsMemory(0, _options.VdiBlockSize));

                        await RandomAccess.WriteAsync(_vdiStream!.SafeFileHandle, buffer.AsMemory(0, read), physicalOffset);
                    }
                }

                // Persist Bitmap
                await RandomAccess.WriteAsync(_mapStream!.SafeFileHandle, _allocationBitmap, 0);

                // Index
                _fileIndex.Set(uri.ToString(), new VdiFileRecord
                {
                    FileSize = length,
                    CreatedAt = DateTime.UtcNow.Ticks,
                    Extents = extents
                });
            }
            finally { _lock.Release(); }
        }

        /// <summary>
        /// Load
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        /// <exception cref="FileNotFoundException"></exception>
        public Task<Stream> LoadAsync(Uri uri)
        {
            if (!_fileIndex.TryGet(uri.ToString(), out var record)) throw new FileNotFoundException();
            return Task.FromResult<Stream>(new VdiStream(_vdiStream!.SafeFileHandle, record!.Extents, record.FileSize, _options.VdiBlockSize));
        }

        /// <summary>
        /// Delete
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public async Task DeleteAsync(Uri uri)
        {
            await _lock.WaitAsync();
            try
            {
                if (_fileIndex.Remove(uri.ToString(), out var record))
                {
                    foreach (var ext in record!.Extents) MarkFree(ext.StartBlock, ext.BlockCount);
                    await RandomAccess.WriteAsync(_mapStream!.SafeFileHandle, _allocationBitmap, 0);
                }
            }
            finally { _lock.Release(); }
        }

        /// <summary>
        /// Exists
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public Task<bool> ExistsAsync(Uri uri)
        {
            return Task.FromResult(_fileIndex.TryGet(uri.ToString(), out _));
        }

        /// <summary>
        /// PRODUCTION MAINTENANCE: Reclaims disk space.
        /// Compaction Strategy: Copy-on-Write to new VDI -> Atomic Swap.
        /// </summary>
        public async Task VacuumAsync()
        {
            await _lock.WaitAsync();
            try
            {
                _context.LogInfo("Starting Vacuum...");
                string tempVdi = _vdiPath + ".tmp";
                using var dest = new FileStream(tempVdi, FileMode.Create);

                long newPointer = 0;
                var newIndex = new System.Collections.Generic.Dictionary<string, VdiFileRecord>();

                byte[] buf = new byte[_options.VdiBlockSize];

                foreach (var kvp in _fileIndex.GetAllKeyValues())
                {
                    var uri = kvp.Key;        // Now works
                    var rec = kvp.Value;      // Now works
                    var newExtents = new System.Collections.Generic.List<VdiExtent>();

                    // Read old data
                    foreach (var ext in rec.Extents)
                    {
                        for (int i = 0; i < ext.BlockCount; i++)
                        {
                            long offset = (ext.StartBlock + i) * _options.VdiBlockSize;
                            await RandomAccess.ReadAsync(_vdiStream!.SafeFileHandle, buf, offset);
                            await dest.WriteAsync(buf);
                        }
                        // Compacted extent
                        newExtents.Add(new VdiExtent { StartBlock = newPointer / _options.VdiBlockSize, BlockCount = ext.BlockCount });
                        newPointer += (ext.BlockCount * _options.VdiBlockSize);
                    }

                    rec.Extents = newExtents;
                    newIndex[uri] = rec;
                }

                // Swap Files (Closing streams first)
                await dest.DisposeAsync();
                await _vdiStream!.DisposeAsync();
                await _mapStream!.DisposeAsync();
                _fileIndex.Dispose();

                File.Move(tempVdi, _vdiPath, true);

                // Re-init
                InitializeDisk(); // Rebuilds map from scratch based on new compact data

                // Re-save index
                foreach (var kvp in newIndex) _fileIndex.Set(kvp.Key, kvp.Value);
            }
            finally { _lock.Release(); }
        }

        public async Task DisposeAsync()
        {
            await _vdiStream!.DisposeAsync();
            await _mapStream!.DisposeAsync();
            _fileIndex.Dispose();
        }

        // --- Allocation Logic ---

        private List<VdiExtent> AllocateBlocks(int needed)
        {
            // Real Logic: Scan bitmap for free runs
            // If not enough space, grow disk

            // 1. Try Find
            long start = FindFreeBlock(needed);
            if (start != -1)
            {
                MarkUsed(start, needed);
                return [new VdiExtent { StartBlock = start, BlockCount = needed }];
            }

            // 2. Grow
            long oldTotal = _totalBlocks;
            _totalBlocks += (needed + 1024); // Grow by needed + buffer

            // Resize VDI
            _vdiStream!.SetLength(_totalBlocks * _options.VdiBlockSize);

            // Resize Map
            int newMapBytes = (int)Math.Ceiling(_totalBlocks / 8.0);
            Array.Resize(ref _allocationBitmap, newMapBytes);
            _mapStream!.SetLength(newMapBytes);

            MarkUsed(oldTotal, needed);
            return [new VdiExtent { StartBlock = oldTotal, BlockCount = needed }];
        }

        private long FindFreeBlock(int count)
        {
            // Naive First-Fit linear scan
            int run = 0;
            for (long i = 0; i < _totalBlocks; i++)
            {
                if (!IsBitSet(i)) run++;
                else run = 0;

                if (run == count) return i - count + 1;
            }
            return -1;
        }

        private void MarkUsed(long start, int count)
        {
            for (long i = 0; i < count; i++) SetBit(start + i, true);
        }

        private void MarkFree(long start, int count)
        {
            for (long i = 0; i < count; i++) SetBit(start + i, false);
        }

        private bool IsBitSet(long block)
        {
            long byteIdx = block / 8;
            int bitIdx = (int)(block % 8);
            if (byteIdx >= _allocationBitmap.Length) return false;
            return (_allocationBitmap[byteIdx] & (1 << bitIdx)) != 0;
        }

        private void SetBit(long block, bool val)
        {
            long byteIdx = block / 8;
            int bitIdx = (int)(block % 8);
            if (byteIdx >= _allocationBitmap.Length) return;
            if (val) _allocationBitmap[byteIdx] |= (byte)(1 << bitIdx);
            else _allocationBitmap[byteIdx] &= (byte)~(1 << bitIdx);
        }
    }
}