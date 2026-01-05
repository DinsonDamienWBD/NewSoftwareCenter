using System;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace DataWarehouse.Kernel.IO
{
    /// <summary>
    /// Diamond Tier IO Utility.
    /// copies data from Source to Destination using System.IO.Pipelines.
    /// This minimizes memory allocation (GC pressure) for large file transfers.
    /// </summary>
    public static class ZeroCopyPipe
    {
        /// <summary>
        /// Copy
        /// </summary>
        /// <param name="source"></param>
        /// <param name="destination"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public static async Task CopyAsync(Stream source, Stream destination, CancellationToken ct)
        {
            var pipe = new Pipe();

            Task writing = FillPipeAsync(source, pipe.Writer, ct);
            Task reading = ReadPipeAsync(destination, pipe.Reader, ct);

            await Task.WhenAll(writing, reading);
        }

        private static async Task FillPipeAsync(Stream source, PipeWriter writer, CancellationToken ct)
        {
            const int minimumBufferSize = 64 * 1024; // 64KB

            while (true)
            {
                // Allocate memory from the Pipe (pooled memory)
                Memory<byte> memory = writer.GetMemory(minimumBufferSize);
                try
                {
                    int bytesRead = await source.ReadAsync(memory, ct);
                    if (bytesRead == 0) break;

                    writer.Advance(bytesRead);
                }
                catch
                {
                    break;
                }

                FlushResult result = await writer.FlushAsync(ct);
                if (result.IsCompleted) break;
            }

            await writer.CompleteAsync();
        }

        private static async Task ReadPipeAsync(Stream destination, PipeReader reader, CancellationToken ct)
        {
            while (true)
            {
                ReadResult result = await reader.ReadAsync(ct);
                System.Buffers.ReadOnlySequence<byte> buffer = result.Buffer;

                foreach (var segment in buffer)
                {
                    await destination.WriteAsync(segment, ct);
                }

                reader.AdvanceTo(buffer.End);

                if (result.IsCompleted) break;
            }

            await reader.CompleteAsync();
        }
    }
}