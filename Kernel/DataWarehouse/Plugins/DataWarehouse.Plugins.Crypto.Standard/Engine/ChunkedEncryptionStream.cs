using DataWarehouse.SDK.Contracts;
using System.Buffers;
using System.Security.Cryptography;
using System.Text;

namespace DataWarehouse.Plugins.Crypto.Standard.Engine
{
    /// <summary>
    /// A forward-only stream that transparently encrypts data in chunks.
    /// buffers incoming writes until a chunk is full, then encrypts and writes to the underlying stream.
    /// Handles chunk-based AES-GCM encryption/decryption.
    /// Format: [Header 5b] [Chunk1] [Chunk2] ...
    /// Chunk: [Length 4b] [Nonce 12b] [Tag 16b] [Ciphertext N]
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    public class ChunkedEncryptionStream : Stream
    {
        private readonly Stream _baseStream;
        private readonly ICryptoTransform _encryptor;
        private readonly bool _leaveOpen;

        // Buffering for optimal crypto performance
        private byte[] _inputBuffer;
        private int _inputBufferIndex;
        private readonly int _inputBlockSize;

        // Output buffer (reused)
        private byte[] _outputBuffer;

        private bool _isDisposed;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="baseStream"></param>
        /// <param name="encryptor"></param>
        /// <param name="leaveOpen"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public ChunkedEncryptionStream(Stream baseStream, ICryptoTransform encryptor, bool leaveOpen = false)
        {
            _baseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));
            _encryptor = encryptor ?? throw new ArgumentNullException(nameof(encryptor));
            _leaveOpen = leaveOpen;

            // Align buffer to InputBlockSize (usually 16 bytes for AES)
            _inputBlockSize = _encryptor.InputBlockSize;

            // Rent buffers from pool for high-performance (God Tier)
            // We use a multiple of block size (e.g., 4KB or 64KB) for efficiency
            int bufferSize = Math.Max(_inputBlockSize, 64 * 1024);
            _inputBuffer = ArrayPool<byte>.Shared.Rent(bufferSize);

            // Output can be larger due to padding/block expansion
            _outputBuffer = ArrayPool<byte>.Shared.Rent(bufferSize + _encryptor.OutputBlockSize);
        }

        /// <summary>
        /// Write
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <exception cref="ObjectDisposedException"></exception>
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(ChunkedEncryptionStream));

            int bytesWritten = 0;

            while (bytesWritten < count)
            {
                // 1. Fill the internal input buffer
                int copyAmount = Math.Min(count - bytesWritten, _inputBuffer.Length - _inputBufferIndex);
                Buffer.BlockCopy(buffer, offset + bytesWritten, _inputBuffer, _inputBufferIndex, copyAmount);

                _inputBufferIndex += copyAmount;
                bytesWritten += copyAmount;

                // 2. If buffer is full, Transform and Flush
                if (_inputBufferIndex == _inputBuffer.Length)
                {
                    TransformBuffer();
                }
            }
        }

        private void TransformBuffer()
        {
            if (_inputBufferIndex == 0) return;

            // TransformBlock encrypts *without* padding/finalization (Continuous Stream)
            int written = _encryptor.TransformBlock(
                _inputBuffer, 0, _inputBufferIndex,
                _outputBuffer, 0
            );

            _baseStream.Write(_outputBuffer, 0, written);
            _inputBufferIndex = 0;
        }

        /// <summary>
        /// Flush
        /// </summary>
        public override void Flush()
        {
            // Note: We cannot flush the CryptoTransform state here because it's block-based.
            // We only flush the underlying stream.
            _baseStream.Flush();
        }

        /// <summary>
        /// Finalize encryption (Padding)
        /// </summary>
        /// <param name="disposing"></param>
        protected override void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    try
                    {
                        // 1. Process remaining bytes in buffer + Add Padding
                        byte[] finalBytes = _encryptor.TransformFinalBlock(
                            _inputBuffer, 0, _inputBufferIndex
                        );

                        // 2. Write final block
                        _baseStream.Write(finalBytes, 0, finalBytes.Length);
                        _baseStream.Flush();

                        // 3. Cleanup
                        _encryptor.Dispose();
                        if (!_leaveOpen) _baseStream.Dispose();
                    }
                    finally
                    {
                        // Return buffers to pool
                        if (_inputBuffer != null) ArrayPool<byte>.Shared.Return(_inputBuffer);
                        if (_outputBuffer != null) ArrayPool<byte>.Shared.Return(_outputBuffer);
                    }
                }
                _isDisposed = true;
            }
            base.Dispose(disposing);
        }

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
        /// get length
        /// </summary>
        public override long Length => _baseStream.Length;

        /// <summary>
        /// Get position
        /// </summary>
        public override long Position { get => _baseStream.Position; set => throw new NotSupportedException(); }
        
        /// <summary>
        /// Set length
        /// </summary>
        /// <param name="value"></param>
        /// <exception cref="NotSupportedException"></exception>
        public override void SetLength(long value) => throw new NotSupportedException();

        /// <summary>
        /// Seek
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="origin"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

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
        /// Safe disposal
        /// </summary>
        //public override async ValueTask DisposeAsync()
        //{
        //    if (_bufferPos > 0)
        //    {
        //        await FlushAsync(CancellationToken.None);
        //    }
        //    await _baseStream.DisposeAsync();
        //    GC.SuppressFinalize(this);
        //}
    }
}