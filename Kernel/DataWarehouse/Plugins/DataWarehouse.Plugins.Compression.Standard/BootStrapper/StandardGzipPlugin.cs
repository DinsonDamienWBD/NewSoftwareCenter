using DataWarehouse.SDK.Contracts;
using DataWarehouse.SDK.IO;
using System.IO.Compression;

namespace DataWarehouse.Plugins.Compression.Standard.Bootstrapper
{
    /// <summary>
    /// Standard GZip plugin
    /// </summary>
    public class StandardGzipPlugin : IFeaturePlugin, IDataTransformation
    {
        /// <summary>
        /// ID
        /// </summary>
        public string Id => "standard-gzip";

        /// <summary>
        /// Name
        /// </summary>
        public string Name => "Standard GZip";

        /// <summary>
        /// Version
        /// </summary>
        public string Version => "1.0";

        /// <summary>
        /// Compression level
        /// </summary>
        public SDK.Contracts.CompressionLevel Level => SDK.Contracts.CompressionLevel.Fast;

        public string Category => "Compression";
        public int QualityLevel => 80; // High compression

        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="context"></param>
        public void Initialize(IKernelContext context) { }

        public Task StartAsync(CancellationToken c) => Task.CompletedTask;
        public Task StopAsync() => Task.CompletedTask;

        /// <summary>
        /// Create compression stream
        /// </summary>
        /// <param name="output"></param>
        /// <returns></returns>
        public Stream CreateCompressionStream(Stream output)
        {
            return new GZipStream(output, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true);
        }

        /// <summary>
        /// Create decompression stream
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public Stream CreateDecompressionStream(Stream input)
        {
            return new GZipStream(input, CompressionMode.Decompress, leaveOpen: true);
        }

        public Stream OnWrite(Stream input, IKernelContext context, Dictionary<string, object> args)
        {
            // On Write, we compress. GZip is Push, so we use Adapter.
            return new PushToPullStreamAdapter(input, (dest) =>
                new GZipStream(dest, System.IO.Compression.CompressionLevel.Fastest, leaveOpen: true));
        }

        public Stream OnRead(Stream stored, IKernelContext context, Dictionary<string, object> args)
        {
            // On Read, we decompress. GZipStream supports Pull (Read) natively.
            return new GZipStream(stored, CompressionMode.Decompress, leaveOpen: true);
        }
    }
}