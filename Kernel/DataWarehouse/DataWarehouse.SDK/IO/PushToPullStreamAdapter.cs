using System.IO.Pipelines;

namespace DataWarehouse.SDK.IO
{
    /// <summary>
    /// Moved from Kernel to SDK so Plugins can use it for "Push" based libraries (like GZip).
    /// </summary>
    public class PushToPullStreamAdapter : Stream
    {
        private readonly Stream _pullSource;
        private readonly Stream _pipeReaderStream;
        private readonly Stream _pipeWriterStream;
        private readonly Task _pumpingTask;
        private readonly CancellationTokenSource _cts = new();

        public PushToPullStreamAdapter(Stream source, Func<Stream, Stream> transformFactory)
        {
            _pullSource = source;
            var pipe = new Pipe(new PipeOptions(pauseWriterThreshold: 64 * 1024, resumeWriterThreshold: 32 * 1024));
            _pipeReaderStream = pipe.Reader.AsStream();
            _pipeWriterStream = pipe.Writer.AsStream();

            var transformStream = transformFactory(_pipeWriterStream);

            _pumpingTask = Task.Run(async () =>
            {
                try
                {
                    await source.CopyToAsync(transformStream, _cts.Token);
                    transformStream.Close();
                    await _pipeWriterStream.FlushAsync();
                }
                catch (Exception ex) { await pipe.Writer.CompleteAsync(ex); }
                finally
                {
                    await pipe.Writer.CompleteAsync();
                    await transformStream.DisposeAsync();
                }
            });
        }

        public override int Read(byte[] buffer, int offset, int count) => _pipeReaderStream.Read(buffer, offset, count);
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) => await _pipeReaderStream.ReadAsync(buffer.AsMemory(offset, count), ct);

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _pullSource.Length;
        public override long Position { get => _pullSource.Position; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cts.Cancel();
                _pipeReaderStream.Dispose();
                try { _pumpingTask.Wait(500); } catch { }
            }
            base.Dispose(disposing);
        }
    }
}
