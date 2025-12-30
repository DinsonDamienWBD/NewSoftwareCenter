namespace DataWarehouse.Drivers
{
    /// <summary>
    /// A wrapper stream that exposes only a specific slice of the underlying stream.
    /// Used for reading "files" embedded inside "segments".
    /// </summary>
    public class SlicedStream(Stream baseStream, long length) : Stream
    {
        private readonly Stream _baseStream = baseStream;
        private readonly long _start = baseStream.Position;
        private readonly long _length = length;
        private long _position = 0;

        /// <summary>
        /// Can read
        /// </summary>
        public override bool CanRead => true;

        /// <summary>
        /// Can seek
        /// </summary>
        public override bool CanSeek => true;

        /// <summary>
        /// Can write
        /// </summary>
        public override bool CanWrite => false;

        /// <summary>
        /// Get length
        /// </summary>
        public override long Length => _length;

        /// <summary>
        /// Position
        /// </summary>
        public override long Position
        {
            get => _position;
            set => Seek(value, SeekOrigin.Begin);
        }

        /// <summary>
        /// Read data
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_position >= _length) return 0;
            int toRead = (int)Math.Min(count, _length - _position);
            int read = _baseStream.Read(buffer, offset, toRead);
            _position += read;
            return read;
        }

        /// <summary>
        /// Read async
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (_position >= _length) return 0;
            int toRead = (int)Math.Min(count, _length - _position);
            int read = await _baseStream.ReadAsync(buffer, offset, toRead, cancellationToken);
            _position += read;
            return read;
        }

        /// <summary>
        /// Seek data
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="origin"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public override long Seek(long offset, SeekOrigin origin)
        {
            long newPos = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => _position + offset,
                SeekOrigin.End => _length + offset,
                _ => throw new ArgumentException("Invalid origin")
            };

            if (newPos < 0 || newPos > _length) throw new ArgumentOutOfRangeException(nameof(offset));
            _position = newPos;
            _baseStream.Seek(_start + _position, SeekOrigin.Begin);
            return _position;
        }

        /// <summary>
        /// Flush
        /// </summary>
        public override void Flush() => _baseStream.Flush();

        /// <summary>
        /// Set length
        /// </summary>
        /// <param name="value"></param>
        /// <exception cref="NotSupportedException"></exception>
        public override void SetLength(long value) => throw new NotSupportedException();

        /// <summary>
        /// Write
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <exception cref="NotSupportedException"></exception>
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}