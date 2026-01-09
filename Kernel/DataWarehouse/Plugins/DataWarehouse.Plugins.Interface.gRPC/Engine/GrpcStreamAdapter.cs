using DataWarehouse.Plugins.Interface.gRPC.Protos;
using DataWarehouse.SDK.AI.Math;
using Grpc.Core;

namespace DataWarehouse.Plugins.Interface.gRPC.Engine
{
    /// <summary>
    /// Adapts a gRPC Server Stream into a .NET Read Stream.
    /// Allows the Kernel to read remote files as if they were local.
    /// Implements true streaming without buffering entire file in memory.
    /// </summary>
    public class GrpcStreamAdapter(AsyncServerStreamingCall<DownloadResponse> call) : Stream
    {
        private readonly AsyncServerStreamingCall<DownloadResponse> _call = call;
        private byte[] _currentBuffer = [];
        private int _currentOffset = 0;
        private bool _isDisposed;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => 0; set => throw new NotSupportedException(); }

        /// <summary>
        /// Read from gRPC stream asynchronously.
        /// </summary>
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            int totalRead = 0;
            while (count > 0)
            {
                // If current buffer exhausted, fetch next chunk
                if (_currentOffset >= _currentBuffer.Length)
                {
                    bool hasNext = await _call.ResponseStream.MoveNext(cancellationToken);
                    if (!hasNext) break; // EOF

                    var chunk = _call.ResponseStream.Current.Chunk;
                    _currentBuffer = chunk.ToByteArray();
                    _currentOffset = 0;
                }

                int available = _currentBuffer.Length - _currentOffset;
                int toCopy = MathUtils.Min(count, available);

                Buffer.BlockCopy(_currentBuffer, _currentOffset, buffer, offset + totalRead, toCopy);

                _currentOffset += toCopy;
                totalRead += toCopy;
                offset += toCopy;
                count -= toCopy;
            }

            return totalRead;
        }

        /// <summary>
        /// Synchronous read (blocks on async).
        /// </summary>
        public override int Read(byte[] buffer, int offset, int count)
            => ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();

        public override void Flush() { }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                _call.Dispose();
                _isDisposed = true;
            }
            base.Dispose(disposing);
        }
    }
}
