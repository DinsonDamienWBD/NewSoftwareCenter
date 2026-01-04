using DataWarehouse.SDK.Contracts;
using System.Buffers;
using System.Security.Cryptography;

namespace DataWarehouse.Plugins.Crypto.Standard.Engine
{
    /// <summary>
    /// A Readable Stream that pulls plaintext from an inner stream, 
    /// encrypts it in chunks, and yields the encrypted bytes.
    /// Eliminates the need for intermediate MemoryStream buffering.
    /// </summary>
    public class ReadableChunkedEncryptionStream : Stream
    {
        private readonly Stream _source;
        private readonly ICryptoTransform _encryptor; // MUST be ICryptoTransform, not ICryptoProvider
        private readonly bool _leaveOpen;

        // Buffers
        private readonly byte[] _inputBuffer;  // Plaintext from source
        private readonly byte[] _outputBuffer; // Ciphertext waiting to be consumed
        private int _outputBufferIndex;
        private int _outputBufferLength;
        private bool _sourceExhausted;
        private bool _isDisposed;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="source"></param>
        /// <param name="encryptor"></param>
        /// <param name="leaveOpen"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public ReadableChunkedEncryptionStream(Stream source, ICryptoTransform encryptor, bool leaveOpen = false)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(encryptor);

            _source = source;
            _encryptor = encryptor;
            _leaveOpen = leaveOpen;

            // Rent buffers relative to block size (e.g. 4KB or 16KB)
            int blockSize = _encryptor.InputBlockSize;
            int bufferSize = Math.Max(blockSize, 16 * 1024); // 16KB chunks

            _inputBuffer = ArrayPool<byte>.Shared.Rent(bufferSize);
            _outputBuffer = ArrayPool<byte>.Shared.Rent(bufferSize + _encryptor.OutputBlockSize);
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
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            if (count == 0) return 0;

            int totalBytesCopied = 0;

            while (totalBytesCopied < count)
            {
                // 1. Serve bytes available in our internal encrypted buffer
                if (_outputBufferIndex < _outputBufferLength)
                {
                    int available = _outputBufferLength - _outputBufferIndex;
                    int toCopy = Math.Min(available, count - totalBytesCopied);

                    Buffer.BlockCopy(_outputBuffer, _outputBufferIndex, buffer, offset + totalBytesCopied, toCopy);

                    _outputBufferIndex += toCopy;
                    totalBytesCopied += toCopy;

                    if (totalBytesCopied == count) return totalBytesCopied;
                }

                // 2. If we are done with source and buffer, we are finished
                if (_sourceExhausted) break;

                // 3. Buffer is empty, fetch more plaintext and encrypt it
                RefillBuffer();
            }

            return totalBytesCopied;
        }

        private void RefillBuffer()
        {
            // Read Plaintext chunk
            int bytesRead = 0;
            // We request data in multiples of InputBlockSize to keep TransformBlock happy until the very end
            int requestSize = _inputBuffer.Length - (_inputBuffer.Length % _encryptor.InputBlockSize);

            // Loop until we get enough data or hit EOF
            while (bytesRead < requestSize)
            {
                int n = _source.Read(_inputBuffer, bytesRead, requestSize - bytesRead);
                if (n == 0) break;
                bytesRead += n;
            }

            // EOF Logic
            if (bytesRead == 0)
            {
                // Finalize Encryption (Padding)
                byte[] finalBytes = _encryptor.TransformFinalBlock([], 0, 0);

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

                _sourceExhausted = true;
                return;
            }

            // Encrypt the chunk
            // TransformBlock writes directly to _outputBuffer and returns count
            int encryptedCount = _encryptor.TransformBlock(
                _inputBuffer, 0, bytesRead,
                _outputBuffer, 0
            );

            _outputBufferIndex = 0;
            _outputBufferLength = encryptedCount;
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
                    _encryptor.Dispose();
                    if (!_leaveOpen) _source.Dispose();

                    if (_inputBuffer != null) ArrayPool<byte>.Shared.Return(_inputBuffer);
                    if (_outputBuffer != null) ArrayPool<byte>.Shared.Return(_outputBuffer);
                }
                _isDisposed = true;
            }
            base.Dispose(disposing);
        }

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
        public override long Length => throw new NotSupportedException();

        /// <summary>
        /// Position
        /// </summary>
        public override long Position { get => _source.Position; set => throw new NotSupportedException(); }

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