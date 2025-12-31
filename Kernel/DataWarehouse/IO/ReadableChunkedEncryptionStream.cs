using DataWarehouse.Contracts;
using System.Security.Cryptography;

namespace DataWarehouse.IO
{
    /// <summary>
    /// A Readable Stream that pulls plaintext from an inner stream, 
    /// encrypts it in chunks, and yields the encrypted bytes.
    /// Eliminates the need for intermediate MemoryStream buffering.
    /// </summary>
    public class ReadableChunkedEncryptionStream(Stream source, ICryptoProvider crypto, byte[] key) : Stream
    {
        private readonly Stream _source = source;
        private readonly ICryptoProvider _crypto = crypto;
        private readonly byte[] _key = key;

        // Buffers
        private readonly byte[] _inputBuffer = new byte[ChunkSize];  // Plaintext chunk (1MB)
        private byte[] _outputBuffer = [];          // Encrypted chunk (1MB + Overhead)

        private int _outputPos;
        private int _outputLength;
        private bool _sourceExhausted;

        private const int ChunkSize = 1024 * 1024; // 1MB Plaintext
        private const int NonceSize = 12;
        private const int TagSize = 16;
        private const int HeaderSize = 4; // Length Int32

        /// <summary>
        /// Read
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int totalCopied = 0;

            while (buffer.Length > 0)
            {
                // 1. Serve from buffer if available
                if (_outputPos < _outputLength)
                {
                    int remaining = _outputLength - _outputPos;
                    int toCopy = Math.Min(remaining, buffer.Length);

                    _outputBuffer.AsSpan(_outputPos, toCopy).CopyTo(buffer.Span);

                    _outputPos += toCopy;
                    totalCopied += toCopy;
                    buffer = buffer[toCopy..];
                    continue;
                }

                // 2. If buffer empty and source exhausted, we are done
                if (_sourceExhausted) break;

                // 3. Replenish Buffer
                await EncryptNextChunkAsync(cancellationToken);
            }

            return totalCopied;
        }

        private async Task EncryptNextChunkAsync(CancellationToken ct)
        {
            // A. Read Plaintext
            int bytesRead = 0;
            while (bytesRead < ChunkSize)
            {
                int n = await _source.ReadAsync(_inputBuffer.AsMemory(bytesRead, ChunkSize - bytesRead), ct);
                if (n == 0) break;
                bytesRead += n;
            }

            if (bytesRead == 0)
            {
                _sourceExhausted = true;
                return;
            }

            // B. Generate Nonce (We control this)
            var nonce = new byte[NonceSize];
            RandomNumberGenerator.Fill(nonce);

            // C. Encrypt via Plugin
            // Contract: Plugin takes (Data, Key, Nonce, AAD). Returns [Tag][Cipher].
            // We assume the plugin strictly adheres to returning the Cipher output.
            // If the plugin acts like AesGcm, it might return [Tag + Cipher] or [Cipher + Tag]. 
            // Standardizing on: Plugin returns payload that needs to be combined with Nonce.

            var plaintextSlice = _inputBuffer.AsSpan(0, bytesRead).ToArray();
            var encryptedPayload = _crypto.Encrypt(plaintextSlice, _key, nonce, []);

            // D. Pack into Output Buffer: [Length 4b] [Nonce 12b] [EncryptedPayload N]
            // Note: We include Nonce here because the Decryptor needs it. 
            // The 'encryptedPayload' from the plugin likely contains the Tag and Ciphertext.

            int packetLength = NonceSize + encryptedPayload.Length;
            int totalSize = HeaderSize + packetLength;

            if (_outputBuffer.Length < totalSize) _outputBuffer = new byte[totalSize];

            // Write Length (of the following block)
            BitConverter.TryWriteBytes(_outputBuffer.AsSpan(0, 4), packetLength);

            // Write Nonce
            nonce.CopyTo(_outputBuffer.AsSpan(4));

            // Write Payload (Tag + Cipher)
            encryptedPayload.CopyTo(_outputBuffer.AsSpan(4 + NonceSize));

            _outputPos = 0;
            _outputLength = totalSize;
        }

        // Boilerplate

        /// <summary>
        /// Read
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => ReadAsync(new Memory<byte>(buffer, offset, count), cancellationToken).AsTask();

        /// <summary>
        /// Read
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public override int Read(byte[] buffer, int offset, int count) => ReadAsync(buffer, offset, count).Result;

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