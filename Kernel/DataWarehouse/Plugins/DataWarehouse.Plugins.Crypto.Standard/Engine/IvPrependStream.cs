using System;
using System.IO;

namespace DataWarehouse.Plugins.Crypto.Standard.Engine
{
    /// <summary>
    /// A stream wrapper that prefixes the stream content with a header (IV).
    /// Read() will yield the IV bytes first, then the underlying stream data.
    /// </summary>
    public class IvPrependStream(Stream source, byte[] header) : Stream
    {
        private readonly Stream _source = source;
        private readonly byte[] _header = header;
        private int _headerPosition = 0;

        /// <summary>
        /// Can read
        /// </summary>
        public override bool CanRead => true;

        /// <summary>
        /// Can seek
        /// </summary>
        public override bool CanSeek => false;

        /// <summary>
        /// Can write
        /// </summary>
        public override bool CanWrite => false;

        /// <summary>
        /// Length
        /// </summary>
        public override long Length => _source.Length + _header.Length;

        /// <summary>
        /// Position
        /// </summary>
        public override long Position { get => 0; set => throw new NotSupportedException(); }

        /// <summary>
        /// Read
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public override int Read(byte[] buffer, int offset, int count)
        {
            int totalBytesRead = 0;

            // 1. Serve Header (IV)
            if (_headerPosition < _header.Length)
            {
                int bytesToCopy = Math.Min(count, _header.Length - _headerPosition);
                Array.Copy(_header, _headerPosition, buffer, offset, bytesToCopy);

                _headerPosition += bytesToCopy;
                totalBytesRead += bytesToCopy;
                offset += bytesToCopy;
                count -= bytesToCopy;
            }

            // 2. Serve Underlying Stream (Ciphertext)
            if (count > 0)
            {
                int bytesFromSource = _source.Read(buffer, offset, count);
                totalBytesRead += bytesFromSource;
            }

            return totalBytesRead;
        }

        /// <summary>
        /// Flush
        /// </summary>
        public override void Flush() => _source.Flush();

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

        /// <summary>
        /// Seek
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="origin"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        /// <summary>
        /// Safely dispose
        /// </summary>
        /// <param name="disposing"></param>
        protected override void Dispose(bool disposing)
        {
            if (disposing) _source.Dispose();
            base.Dispose(disposing);
        }
    }
}