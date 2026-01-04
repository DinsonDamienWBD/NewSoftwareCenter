using System.Security.Cryptography;

namespace DataWarehouse.Kernel.IO
{
    /// <summary>
    /// A stream that writes to a primary destination while simultaneously
    /// feeding data into a HashAlgorithm. Allows hashing non-seekable streams on the fly.
    /// </summary>
    public class TeeStream(Stream sink, HashAlgorithm hasher) : Stream
    {
        private readonly Stream _sink = sink;
        private readonly HashAlgorithm _hasher = hasher;

        /// <summary>
        /// Write
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            // 1. Update Hash
            _hasher.TransformBlock(buffer, offset, count, null, 0);

            // 2. Write to Disk/Network
            await _sink.WriteAsync(buffer, offset, count, cancellationToken);
        }

        /// <summary>
        /// Write
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            // 1. Update Hash
            // Note: TransformBlock doesn't support Span/Memory natively in older .NET, 
            // but for .NET 10/Standard 2.1 we can use an array rental or unsafe.
            // Safe fallback:
            var array = System.Buffers.ArrayPool<byte>.Shared.Rent(buffer.Length);
            try
            {
                buffer.CopyTo(array);
                _hasher.TransformBlock(array, 0, buffer.Length, null, 0);

                // 2. Write to Sink
                await _sink.WriteAsync(buffer, cancellationToken);
            }
            finally
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(array);
            }
        }

        /// <summary>
        /// Write
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        public override void Write(byte[] buffer, int offset, int count)
        {
            _hasher.TransformBlock(buffer, offset, count, null, 0);
            _sink.Write(buffer, offset, count);
        }

        // Boilerplate

        /// <summary>
        /// Can read
        /// </summary>
        public override bool CanRead => false;

        /// <summary>
        /// Can seek
        /// </summary>
        public override bool CanSeek => false;

        /// <summary>
        /// Can write
        /// </summary>
        public override bool CanWrite => true;

        /// <summary>
        /// Length
        /// </summary>
        public override long Length => _sink.Length;

        /// <summary>
        /// Position
        /// </summary>
        public override long Position { get => _sink.Position; set => throw new NotSupportedException(); }

        /// <summary>
        /// Flush
        /// </summary>
        public override void Flush() => _sink.Flush();

        /// <summary>
        /// Read
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        /// <summary>
        /// Seek
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="origin"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        /// <summary>
        /// Set length
        /// </summary>
        /// <param name="value"></param>
        /// <exception cref="NotSupportedException"></exception>
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}