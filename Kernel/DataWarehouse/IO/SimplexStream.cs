using System.IO.Pipelines;

namespace DataWarehouse.IO
{
    /// <summary>
    /// Bridges a "Push" producer (like GZipStream) to a "Pull" consumer (like SaveAsync).
    /// </summary>
    public class SimplexStream : Stream
    {
        private readonly Pipe _pipe;
        private readonly Task _producerTask;
        private readonly Stream _pipeReadStream;
        private bool _disposed;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="producerAction"></param>
        public SimplexStream(Func<Stream, CancellationToken, Task> producerAction)
        {
            _pipe = new Pipe();
            _pipeReadStream = _pipe.Reader.AsStream();

            // Start the producer in background
            _producerTask = Task.Run(async () =>
            {
                var writeStream = _pipe.Writer.AsStream();
                try
                {
                    await producerAction(writeStream, CancellationToken.None);
                    await writeStream.FlushAsync();
                    await _pipe.Writer.CompleteAsync();
                }
                catch (Exception ex)
                {
                    await _pipe.Writer.CompleteAsync(ex);
                }
            });
        }

        /// <summary>
        /// Read
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return await _pipeReadStream.ReadAsync(buffer, cancellationToken);
        }

        /// <summary>
        /// Read
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public override int Read(byte[] buffer, int offset, int count)
            => _pipeReadStream.Read(buffer, offset, count);

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
        public override long Length => throw new NotSupportedException();

        /// <summary>
        /// Position
        /// </summary>
        public override long Position { get => 0; set => throw new NotSupportedException(); }

        /// <summary>
        /// Flush
        /// </summary>
        public override void Flush() { }

        /// <summary>
        /// Set length
        /// </summary>
        /// <param name="value"></param>
        public override void SetLength(long value) { }

        /// <summary>
        /// Write
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        public override void Write(byte[] buffer, int offset, int count) { }

        /// <summary>
        /// Seek
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="origin"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        /// <summary>
        /// Safe dispose
        /// </summary>
        /// <param name="disposing"></param>
        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;
                // Wait for producer to finish or cancel? 
                // For safety we typically let GC handle the Task, or we could cancel a CTS.
            }
            base.Dispose(disposing);
        }
    }
}