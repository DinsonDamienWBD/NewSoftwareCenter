using DataWarehouse.Plugins.Features.EnterpriseStorage.Protos;
using Grpc.Core;

namespace DataWarehouse.Plugins.Features.EnterpriseStorage.Engine
{
    /// <summary>
    /// Adapts a gRPC Server Stream into a .NET Read Stream.
    /// Allows the Kernel to read remote files as if they were local.
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

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            // 1. If we have leftover bytes in current buffer, consume them
            int totalRead = 0;
            while (count > 0)
            {
                if (_currentOffset >= _currentBuffer.Length)
                {
                    // Fetch next chunk from network
                    bool hasNext = await _call.ResponseStream.MoveNext(cancellationToken);
                    if (!hasNext) break; // EOF

                    var chunk = _call.ResponseStream.Current.Chunk;
                    _currentBuffer = chunk.ToByteArray();
                    _currentOffset = 0;
                }

                int available = _currentBuffer.Length - _currentOffset;
                int toCopy = Math.Min(count, available);

                Buffer.BlockCopy(_currentBuffer, _currentOffset, buffer, offset + totalRead, toCopy);

                _currentOffset += toCopy;
                totalRead += toCopy;
                offset += toCopy;
                count -= toCopy;
            }

            return totalRead;
        }

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