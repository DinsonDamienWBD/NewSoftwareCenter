using DataWarehouse.Plugins.Features.Consensus.Engine;
using DataWarehouse.Plugins.Features.Consensus.Protos;
using DataWarehouse.SDK.Contracts;
using Grpc.Core;
using System;
using System.IO;
using System.Threading.Tasks;

namespace DataWarehouse.Plugins.Features.Consensus.Services
{
    /// <summary>
    /// The Network Interface for Raft.
    /// Accepts gRPC calls from peers and forwards them to the Raft Engine.
    /// </summary>
    public class RaftGrpcService(Engine.RaftEngine engine, IStorageProvider storage) : StorageTransport.StorageTransportBase
    {
        private readonly Engine.RaftEngine _engine = engine;
        private readonly IStorageProvider _storage = storage;

        // 1. Vote Request (Leader Election)
        // --- Consensus Methods (Raft) ---

        public override Task<VoteResponse> RequestVote(VoteRequest request, ServerCallContext context)
        {
            return Task.FromResult(_engine.HandleRequestVote(request));
        }

        public override Task<AppendResponse> AppendEntries(AppendRequest request, ServerCallContext context)
        {
            return Task.FromResult(_engine.HandleAppendEntries(request));
        }

        // --- Data Plane Methods (Storage) ---

        public override async Task<UploadResponse> UploadBlob(IAsyncStreamReader<UploadRequest> requestStream, ServerCallContext context)
        {
            try
            {
                // 1. Read Metadata (First Message)
                if (!await requestStream.MoveNext() || requestStream.Current.PayloadCase != UploadRequest.PayloadOneofCase.Metadata)
                {
                    return new UploadResponse { Success = false, Message = "Protocol Error: Metadata expected first." };
                }

                var metadata = requestStream.Current.Metadata;
                var uri = new Uri(metadata.Uri);

                // 2. Stream Data to Local Storage
                // We wrap the gRPC stream in our adapter so _storage can read from it like a file
                using var readStream = new GrpcUploadStreamAdapter(requestStream);

                await _storage.SaveAsync(uri, readStream);

                return new UploadResponse { Success = true };
            }
            catch (Exception ex)
            {
                return new UploadResponse { Success = false, Message = ex.Message };
            }
        }

        public override async Task DownloadBlob(DownloadRequest request, IServerStreamWriter<DownloadResponse> responseStream, ServerCallContext context)
        {
            try
            {
                var uri = new Uri(request.Uri);

                // 1. Get Local Stream
                using var fileStream = await _storage.LoadAsync(uri);

                // 2. Chunk and Stream to Client
                byte[] buffer = new byte[64 * 1024]; // 64KB chunks
                int bytesRead;

                while ((bytesRead = await fileStream.ReadAsync(buffer)) > 0)
                {
                    await responseStream.WriteAsync(new DownloadResponse
                    {
                        Chunk = Google.Protobuf.ByteString.CopyFrom(buffer, 0, bytesRead)
                    });
                }
            }
            catch (FileNotFoundException)
            {
                // gRPC doesn't have a 404, usually we throw RpcException
                throw new RpcException(new Status(StatusCode.NotFound, $"Blob {request.Uri} not found."));
            }
            catch (Exception ex)
            {
                throw new RpcException(new Status(StatusCode.Internal, ex.Message));
            }
        }

        public override async Task<ExistsResponse> ExistsBlob(ExistsRequest request, ServerCallContext context)
        {
            var exists = await _storage.ExistsAsync(new Uri(request.Uri));
            return new ExistsResponse { Exists = exists };
        }

        public override async Task<DeleteResponse> DeleteBlob(DeleteRequest request, ServerCallContext context)
        {
            try
            {
                await _storage.DeleteAsync(new Uri(request.Uri));
                return new DeleteResponse { Success = true };
            }
            catch
            {
                return new DeleteResponse { Success = false };
            }
        }
    }

    /// <summary>
    /// Wraps a gRPC Client Streaming Request into a readable .NET Stream.
    /// Used by the Server to read data being uploaded by a Client.
    /// </summary>
    public class GrpcUploadStreamAdapter(IAsyncStreamReader<UploadRequest> reader) : Stream
    {
        private readonly IAsyncStreamReader<UploadRequest> _reader = reader;
        private byte[] _currentBuffer = [];
        private int _currentOffset = 0;
        private bool _isExhausted = false;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => 0; set => throw new NotSupportedException(); }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int totalBytesCopied = 0;

            while (buffer.Length > 0)
            {
                // 1. Consume existing buffer
                if (_currentOffset < _currentBuffer.Length)
                {
                    int available = _currentBuffer.Length - _currentOffset;
                    int toCopy = Math.Min(buffer.Length, available);

                    // Efficient Span Copy (No allocation)
                    new ReadOnlySpan<byte>(_currentBuffer, _currentOffset, toCopy).CopyTo(buffer.Span);

                    _currentOffset += toCopy;
                    buffer = buffer[toCopy..]; // Advance the destination window
                    totalBytesCopied += toCopy;
                }
                else
                {
                    // 2. Fetch next chunk from Network
                    if (_isExhausted || !await _reader.MoveNext(cancellationToken))
                    {
                        _isExhausted = true;
                        break; // End of Stream
                    }

                    // Only process Chunk payloads
                    if (_reader.Current.PayloadCase == UploadRequest.PayloadOneofCase.Chunk)
                    {
                        // Note: ToByteArray() allocates, but dealing with raw ByteString memory 
                        // requires Unsafe access. This is the safe standard.
                        _currentBuffer = _reader.Current.Chunk.ToByteArray();
                        _currentOffset = 0;
                    }
                }
            }

            return totalBytesCopied;
        }

        // [FIX] CA1844: Redirect Legacy Array call to Memory implementation
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return ReadAsync(new Memory<byte>(buffer, offset, count), cancellationToken).AsTask();
        }

        // [FIX] Sync Read calls Async implementation (common pattern for wrappers)
        public override int Read(byte[] buffer, int offset, int count)
        {
            return ReadAsync(buffer, offset, count).GetAwaiter().GetResult();
        }
        public override void Flush() { }
        public override void SetLength(long value) { }
        public override void Write(byte[] buffer, int offset, int count) { }
        public override long Seek(long offset, SeekOrigin origin) => 0;
    }
}