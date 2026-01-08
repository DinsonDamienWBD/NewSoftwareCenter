using System.IO.Compression;

namespace DataWarehouse.Plugins.Compression.Standard.Engine
{
    /// <summary>
    /// Core GZip compression/decompression engine.
    /// Implements the actual algorithm logic without any plugin plumbing.
    ///
    /// This engine is stateless and thread-safe.
    /// Uses .NET's built-in GZipStream which implements RFC 1952.
    /// </summary>
    internal static class GZipEngine
    {
        // =========================================================================
        // COMPRESSION
        // =========================================================================

        /// <summary>
        /// Compresses data using GZip algorithm.
        /// </summary>
        /// <param name="input">Raw uncompressed data.</param>
        /// <param name="args">Optional arguments: "level" = "fastest"/"optimal"/"nocompression".</param>
        /// <returns>Compressed data.</returns>
        public static async Task<byte[]> CompressAsync(byte[] input, Dictionary<string, object> args)
        {
            // Determine compression level from arguments
            var compressionLevel = GetCompressionLevel(args);

            // Compress using GZipStream
            using var outputStream = new MemoryStream();
            using (var gzipStream = new GZipStream(outputStream, compressionLevel, leaveOpen: true))
            {
                await gzipStream.WriteAsync(input, 0, input.Length);
            }

            // Return compressed bytes
            return outputStream.ToArray();
        }

        // =========================================================================
        // DECOMPRESSION
        // =========================================================================

        /// <summary>
        /// Decompresses GZip-compressed data.
        /// </summary>
        /// <param name="input">Compressed GZip data.</param>
        /// <param name="args">Optional arguments (currently unused).</param>
        /// <returns>Original uncompressed data.</returns>
        public static async Task<byte[]> DecompressAsync(byte[] input, Dictionary<string, object> args)
        {
            // Decompress using GZipStream
            using var inputStream = new MemoryStream(input);
            using var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress, leaveOpen: false);
            using var outputStream = new MemoryStream();

            // Copy decompressed data to output
            await gzipStream.CopyToAsync(outputStream);

            // Return decompressed bytes
            return outputStream.ToArray();
        }

        // =========================================================================
        // HELPER METHODS
        // =========================================================================

        /// <summary>
        /// Extracts compression level from arguments.
        /// Defaults to Fastest for optimal throughput.
        /// </summary>
        /// <param name="args">Argument dictionary.</param>
        /// <returns>Compression level.</returns>
        private static CompressionLevel GetCompressionLevel(Dictionary<string, object> args)
        {
            if (args.ContainsKey("level") && args["level"] is string levelStr)
            {
                return levelStr.ToLowerInvariant() switch
                {
                    "fastest" => CompressionLevel.Fastest,
                    "optimal" => CompressionLevel.Optimal,
                    "nocompression" => CompressionLevel.NoCompression,
                    _ => CompressionLevel.Fastest // Default
                };
            }

            // Default to fastest for best throughput
            return CompressionLevel.Fastest;
        }
    }
}
