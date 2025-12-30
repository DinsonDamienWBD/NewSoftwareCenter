using DataWarehouse.Contracts;

namespace DataWarehouse.IO
{
    /// <summary>
    /// Reads data written by ChunkedEncryptionStream.
    /// Parses [Length][Nonce][Tag][Ciphertext] blocks and decrypts them on the fly.
    /// </summary>
    public class ChunkedDecryptionStream(Stream baseStream, ICryptoProvider crypto, byte[] key) : Stream
    {
        private readonly Stream _baseStream = baseStream;
        private readonly ICryptoProvider _crypto = crypto;
        private readonly byte[] _key = key;

        private byte[] _currentDecryptedBlock = [];
        private int _blockOffset = 0;
        private bool _eof = false;

        private const int NonceSize = 12;
        private const int TagSize = 16;
        private const int HeaderSize = 4;

        /// <summary>
        ///  Read data
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int totalBytesRead = 0;

            while (buffer.Length > 0)
            {
                // 1. If we have decrypted bytes available, serve them
                int available = _currentDecryptedBlock.Length - _blockOffset;
                if (available > 0)
                {
                    int toCopy = Math.Min(available, buffer.Length);

                    // Copy from internal buffer to destination Memory<byte>
                    _currentDecryptedBlock.AsSpan(_blockOffset, toCopy).CopyTo(buffer.Span);

                    _blockOffset += toCopy;
                    buffer = buffer[toCopy..]; // Advance destination
                    totalBytesRead += toCopy;
                    continue;
                }

                // 2. If buffer empty and we hit EOF previously, we are done
                if (_eof) break;

                // 3. Fetch next chunk from disk
                await FetchNextChunkAsync(cancellationToken);
            }

            return totalBytesRead;
        }

        /// <summary>
        /// Route array-based calls to the Memory-based implementation
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return ReadAsync(new Memory<byte>(buffer, offset, count), cancellationToken).AsTask();
        }

        private async Task FetchNextChunkAsync(CancellationToken ct)
        {
            // Read Length Header (4 bytes)
            var lengthBuffer = new byte[HeaderSize];
            int read = await ReadFullAsync(_baseStream, lengthBuffer, HeaderSize, ct);

            if (read == 0)
            {
                _eof = true;
                return;
            }

            int chunkPayloadLength = BitConverter.ToInt32(lengthBuffer, 0);

            // Read Full Payload (Nonce + Tag + Ciphertext)
            var payloadBuffer = new byte[chunkPayloadLength];
            read = await ReadFullAsync(_baseStream, payloadBuffer, chunkPayloadLength, ct);
            if (read != chunkPayloadLength) throw new EndOfStreamException("Corrupt encrypted file: incomplete chunk.");

            // FIX: CS7036 - Extract Components manually
            // Format: [Nonce 12] [Tag 16] [Ciphertext N]

            if (chunkPayloadLength < NonceSize + TagSize)
                throw new InvalidDataException("Chunk too small to contain security headers.");

            var payloadSpan = payloadBuffer.AsSpan();
            var nonce = payloadSpan[..NonceSize].ToArray();
            var tag = payloadSpan.Slice(NonceSize, TagSize).ToArray();
            var ciphertext = payloadSpan[(NonceSize + TagSize)..].ToArray();

            // Pass explicit arguments to Decrypt
            _currentDecryptedBlock = _crypto.Decrypt(ciphertext, _key, nonce, tag);
            _blockOffset = 0;
        }

        private static async Task<int> ReadFullAsync(Stream s, Memory<byte> buffer, int count, CancellationToken ct)
        {
            int total = 0;
            while (total < count)
            {
                int read = await s.ReadAsync(buffer[total..count], ct);
                if (read == 0) return total;
                total += read;
            }
            return total;
        }

        private static async Task<int> ReadFullAsync(Stream s, byte[] buffer, int count, CancellationToken ct)
        {
            return await ReadFullAsync(s, new Memory<byte>(buffer), count, ct);
        }

        // Boilerplate

        /// <summary>
        /// Read
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public override int Read(byte[] buffer, int offset, int count)
            => ReadAsync(buffer, offset, count).Result;

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