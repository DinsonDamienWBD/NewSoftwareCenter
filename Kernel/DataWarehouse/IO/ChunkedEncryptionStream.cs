using Core.Security;
using DataWarehouse.Contracts;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace DataWarehouse.IO
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
    public class ChunkedEncryptionStream(Stream baseStream, ICryptoProvider crypto, byte[] key, string contextId) : Stream
    {
        private readonly Stream _baseStream = baseStream;
        private readonly ICryptoProvider _crypto = crypto;
        private readonly byte[] _key = key;
        private readonly byte[] _contextId = Encoding.UTF8.GetBytes(contextId);
        private readonly byte[] _buffer = new byte[ChunkSize];
        private int _bufferPos = 0;
        private int _chunkIndex = 0;
        private const int ChunkSize = 1024 * 1024; // 1MB

        /// <summary>
        /// BLOCKING WRITE: Explicitly forbidden to prevent ThreadPool starvation
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <exception cref="NotSupportedException"></exception>
        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotSupportedException("Synchronous writes are disabled. Use WriteAsync.");

        /// <summary>
        /// Write async
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => WriteAsync(new ReadOnlyMemory<byte>(buffer, offset, count), cancellationToken).AsTask();

        /// <summary>
        /// Write async
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int bytesWritten = 0;
            while (bytesWritten < buffer.Length)
            {
                int spaceInBuffer = ChunkSize - _bufferPos;
                int bytesToCopy = Math.Min(spaceInBuffer, buffer.Length - bytesWritten);

                buffer.Slice(bytesWritten, bytesToCopy).CopyTo(_buffer.AsMemory(_bufferPos));

                _bufferPos += bytesToCopy;
                bytesWritten += bytesToCopy;

                if (_bufferPos == ChunkSize)
                {
                    await FlushBufferAsync(cancellationToken);
                }
            }
        }

        /// <summary>
        /// Flush to disk
        /// </summary>
        /// <exception cref="NotSupportedException"></exception>
        public override void Flush()
            => throw new NotSupportedException("Synchronous flush is disabled. Use FlushAsync.");

        /// <summary>
        /// Flush async
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            if (_bufferPos > 0)
            {
                await FlushBufferAsync(cancellationToken);
            }
            await _baseStream.FlushAsync(cancellationToken);
        }

        private async Task FlushBufferAsync(CancellationToken ct)
        {
            if (_bufferPos == 0) return;

            // 1. Generate Unique Random Nonce
            var nonce = new byte[12];
            RandomNumberGenerator.Fill(nonce);

            // 2. Construct AAD (Context ID + Chunk Index)
            // This prevents moving chunks between files OR reordering chunks within a file.
            byte[] chunkIndexBytes = BitConverter.GetBytes(_chunkIndex);
            byte[] aad = new byte[_contextId.Length + chunkIndexBytes.Length];
            Buffer.BlockCopy(_contextId, 0, aad, 0, _contextId.Length);
            Buffer.BlockCopy(chunkIndexBytes, 0, aad, _contextId.Length, chunkIndexBytes.Length);

            // 3. Encrypt
            var chunkData = _buffer.AsSpan(0, _bufferPos).ToArray();
            var encryptedPayload = _crypto.Encrypt(chunkData, _key, nonce, aad);

            // 4. Write Packet: [Length 4][Nonce 12][Payload N]
            int packetSize = 12 + encryptedPayload.Length;
            await _baseStream.WriteAsync(BitConverter.GetBytes(packetSize), ct);
            await _baseStream.WriteAsync(nonce, ct);
            await _baseStream.WriteAsync(encryptedPayload, ct);

            _bufferPos = 0;
            _chunkIndex++;
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
        public override async ValueTask DisposeAsync()
        {
            if (_bufferPos > 0)
            {
                await FlushAsync(CancellationToken.None);
            }
            await _baseStream.DisposeAsync();
            GC.SuppressFinalize(this);
        }
    }
}