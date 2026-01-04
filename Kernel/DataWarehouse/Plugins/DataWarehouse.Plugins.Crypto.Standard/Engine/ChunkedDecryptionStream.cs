using DataWarehouse.SDK.Contracts;
using System.Buffers;
using System.Security.Cryptography;

namespace DataWarehouse.Plugins.Crypto.Standard.Engine
{
    /// <summary>
    /// Reads data written by ChunkedEncryptionStream.
    /// Parses [Length][Nonce][Tag][Ciphertext] blocks and decrypts them on the fly.
    /// </summary>
    public class ChunkedDecryptionStream : Stream
    {
        private readonly Stream _baseStream;
        private readonly ICryptoTransform _decryptor;
        private readonly bool _leaveOpen;

        private byte[] _inputBuffer;
        private byte[] _outputBuffer;
        private int _outputBufferIndex;
        private int _outputBufferLength;

        private bool _isDisposed;
        private bool _endOfStream;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="baseStream"></param>
        /// <param name="decryptor"></param>
        /// <param name="leaveOpen"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public ChunkedDecryptionStream(Stream baseStream, ICryptoTransform decryptor, bool leaveOpen = false)
        {
            _baseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));
            _decryptor = decryptor ?? throw new ArgumentNullException(nameof(decryptor));
            _leaveOpen = leaveOpen;

            // Buffers (e.g. 4KB chunks)
            int bufferSize = Math.Max(_decryptor.InputBlockSize, 4096);
            _inputBuffer = ArrayPool<byte>.Shared.Rent(bufferSize);
            _outputBuffer = ArrayPool<byte>.Shared.Rent(bufferSize + _decryptor.OutputBlockSize); // +Padding room
        }

        /// <summary>
        /// Read
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        /// <exception cref="ObjectDisposedException"></exception>
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(ChunkedDecryptionStream));
            if (count == 0) return 0;

            int totalBytesRead = 0;

            while (totalBytesRead < count)
            {
                // 1. Serve bytes from the already decrypted buffer
                if (_outputBufferIndex < _outputBufferLength)
                {
                    int bytesAvailable = _outputBufferLength - _outputBufferIndex;
                    int bytesToCopy = Math.Min(bytesAvailable, count - totalBytesRead);

                    Buffer.BlockCopy(_outputBuffer, _outputBufferIndex, buffer, offset + totalBytesRead, bytesToCopy);

                    _outputBufferIndex += bytesToCopy;
                    totalBytesRead += bytesToCopy;

                    if (totalBytesRead == count) return totalBytesRead;
                }

                // 2. If buffer empty and base stream ended, we are done
                if (_endOfStream) break;

                // 3. Buffer empty? Fetch more encrypted data and transform
                RefillBuffer();
            }

            return totalBytesRead;
        }

        /// <summary>
        /// Refill buffer
        /// </summary>
        private void RefillBuffer()
        {
            // Read a chunk from the underlying stream
            // IMPORTANT: We must read a multiple of InputBlockSize unless it's the end
            int bytesRead = 0;
            int requestSize = _inputBuffer.Length;

            // Ensure we read at least one block if possible, or whatever is left
            while (bytesRead < requestSize)
            {
                int n = _baseStream.Read(_inputBuffer, bytesRead, requestSize - bytesRead);
                if (n == 0) break;
                bytesRead += n;
            }

            if (bytesRead == 0)
            {
                // Finalize Decryption (Remove Padding)
                byte[] finalBytes = _decryptor.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

                // Copy final bytes to output buffer
                if (finalBytes.Length > 0)
                {
                    Buffer.BlockCopy(finalBytes, 0, _outputBuffer, 0, finalBytes.Length);
                    _outputBufferIndex = 0;
                    _outputBufferLength = finalBytes.Length;
                }
                else
                {
                    _outputBufferLength = 0;
                }

                _endOfStream = true;
                return;
            }

            // Transform Continuous Block
            // Note: TransformBlock returns the number of bytes written to output
            int decryptedCount = _decryptor.TransformBlock(
                _inputBuffer, 0, bytesRead,
                _outputBuffer, 0
            );

            _outputBufferIndex = 0;
            _outputBufferLength = decryptedCount;
        }

        /// <summary>
        /// Safely dispose
        /// </summary>
        /// <param name="disposing"></param>
        protected override void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    _decryptor.Dispose();
                    if (!_leaveOpen) _baseStream.Dispose();

                    if (_inputBuffer != null) ArrayPool<byte>.Shared.Return(_inputBuffer);
                    if (_outputBuffer != null) ArrayPool<byte>.Shared.Return(_outputBuffer);
                }
                _isDisposed = true;
            }
            base.Dispose(disposing);
        }

        // Boilerplate

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
        public override long Length => _baseStream.Length; // Approximation

        /// <summary>
        /// Position
        /// </summary>
        public override long Position { get => _baseStream.Position; set => throw new NotSupportedException(); }

        /// <summary>
        /// Flush
        /// </summary>
        public override void Flush() { }

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
    }
}