using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DataWarehouse.Plugins.Compression.Standard.Engine;
using DataWarehouse.SDK.Contracts;
using DataWarehouse.SDK.Contracts.CategoryBases;

namespace DataWarehouse.Plugins.Compression.Standard.Bootstrapper
{
    /// <summary>
    /// GZip compression plugin using the standardized pipeline base.
    /// Provides fast compression/decompression using the GZip algorithm (RFC 1952).
    ///
    /// This plugin is hardware-accelerated where available and optimized for speed.
    /// Compression ratio: ~3-4x for text, ~1.5-2x for binary.
    /// </summary>
    public class GZipCompressionPlugin : PipelinePluginBase
    {
        // =========================================================================
        // CONSTRUCTOR - Sets metadata once
        // =========================================================================

        /// <summary>
        /// Constructs the GZip compression plugin with standardized metadata.
        /// </summary>
        public GZipCompressionPlugin()
            : base(
                id: "DataWarehouse.Pipeline.GZip",
                name: "GZip Compression",
                version: new Version(2, 0, 0)) // Incremented to v2.0.0 for new architecture
        {
        }

        // =========================================================================
        // TRANSFORMATION TYPE - Identifies this pipeline plugin
        // =========================================================================

        /// <summary>
        /// Transformation type identifier for capability generation.
        /// Generates capabilities: "transform.gzip.apply" and "transform.gzip.reverse"
        /// </summary>
        protected override string TransformType => "gzip";

        // =========================================================================
        // FORWARD TRANSFORMATION - Compress data
        // =========================================================================

        /// <summary>
        /// Applies GZip compression to input data.
        /// Uses fastest compression level for optimal throughput.
        /// </summary>
        /// <param name="input">Raw uncompressed data.</param>
        /// <param name="args">Compression arguments (optional "level": "fastest"/"optimal"/"nocompression").</param>
        /// <returns>Compressed data as byte array.</returns>
        protected override async Task<byte[]> ApplyTransformAsync(byte[] input, Dictionary<string, object> args)
        {
            // Delegate to engine
            return await GZipEngine.CompressAsync(input, args);
        }

        // =========================================================================
        // REVERSE TRANSFORMATION - Decompress data
        // =========================================================================

        /// <summary>
        /// Reverses GZip compression (decompresses data).
        /// Automatically detects and handles GZip format.
        /// </summary>
        /// <param name="input">Compressed GZip data.</param>
        /// <param name="args">Decompression arguments (currently unused).</param>
        /// <returns>Original uncompressed data as byte array.</returns>
        protected override async Task<byte[]> ReverseTransformAsync(byte[] input, Dictionary<string, object> args)
        {
            // Delegate to engine
            return await GZipEngine.DecompressAsync(input, args);
        }

        // =========================================================================
        // CUSTOM INITIALIZATION (optional)
        // =========================================================================

        /// <summary>
        /// Custom pipeline initialization (optional override).
        /// Currently no additional initialization needed for GZip.
        /// </summary>
        /// <param name="context">Kernel context.</param>
        protected override void InitializePipeline(IKernelContext context)
        {
            // GZip needs no special initialization
            // Algorithm is stateless and works out of the box
            context.LogInfo("GZip compression plugin initialized (RFC 1952 compliant)");
        }
    }
}
