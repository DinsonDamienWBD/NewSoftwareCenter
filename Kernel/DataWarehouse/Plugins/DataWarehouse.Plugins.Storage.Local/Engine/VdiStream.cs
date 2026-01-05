using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace DataWarehouse.Plugins.Storage.LocalFileSystem.Engine
{
    /// <summary>
    /// A Read-Only stream that stitches together fragmented Extents from the VDI.
    /// Uses RandomAccess for high-performance, lock-free reads.
    /// </summary>
    /// <remarks>
    /// Initializes a new VdiStream.
    /// </remarks>
    public class VdiStream(SafeFileHandle handle, List<VdiExtent> extents, long fileLength, int blockSize) : Stream
    {
        private readonly SafeFileHandle _handle = handle;
        private readonly List<VdiExtent> _extents = extents;
        private readonly long _fileLength = fileLength;
        private readonly int _blockSize = blockSize;
        private long _position = 0;

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _fileLength;

        public override long Position
        {
            get => _position;
            set
            {
                if (value < 0 || value > _fileLength) throw new ArgumentOutOfRangeException(nameof(value));
                _position = value;
            }
        }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count)
        {
            // Sync wrapper for Async
            return ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (_position >= _fileLength) return 0;

            int totalRead = 0;
            int remaining = Math.Min(count, (int)(_fileLength - _position));

            while (remaining > 0)
            {
                // 1. Map logical _position to Physical Offset
                (long physicalOffset, int bytesAvailableInBlock) = MapLogicalToPhysical(_position);

                int toRead = Math.Min(remaining, bytesAvailableInBlock);

                // 2. Perform Random Read
                // Note: We use AsMemory because RandomAccess requires it
                int bytesRead = await RandomAccess.ReadAsync(
                    _handle,
                    buffer.AsMemory(offset + totalRead, toRead),
                    physicalOffset,
                    cancellationToken);

                if (bytesRead == 0) break; // Unexpected EOF

                // 3. Advance cursors
                _position += bytesRead;
                totalRead += bytesRead;
                remaining -= bytesRead;
            }

            return totalRead;
        }

        /// <summary>
        /// Maps the current logical byte position to the physical byte offset on disk.
        /// </summary>
        private (long physicalOffset, int bytesAvailableInBlock) MapLogicalToPhysical(long logicalPos)
        {
            long currentBlockIndex = logicalPos / _blockSize;
            int offsetInBlock = (int)(logicalPos % _blockSize);

            long blocksSkipped = 0;

            foreach (var extent in _extents)
            {
                long extentEnd = blocksSkipped + extent.BlockCount;

                if (currentBlockIndex < extentEnd)
                {
                    // Found the extent containing this block
                    long offsetInExtent = currentBlockIndex - blocksSkipped;
                    long physicalBlock = extent.StartBlock + offsetInExtent;
                    long physicalByteOffset = (physicalBlock * _blockSize) + offsetInBlock;

                    int bytesLeftInBlock = _blockSize - offsetInBlock;
                    return (physicalByteOffset, bytesLeftInBlock);
                }

                blocksSkipped += extent.BlockCount;
            }

            throw new EndOfStreamException("Logical position is outside the mapped extents.");
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long newPos = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => _position + offset,
                SeekOrigin.End => _fileLength + offset,
                _ => throw new ArgumentException("Invalid Origin")
            };

            if (newPos < 0) throw new IOException("Seek before begin");
            _position = newPos; // We allow seeking past end (Read will return 0)
            return _position;
        }

        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}