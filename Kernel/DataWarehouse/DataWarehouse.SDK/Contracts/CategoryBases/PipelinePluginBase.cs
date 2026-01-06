using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace DataWarehouse.SDK.Contracts.CategoryBases
{
    /// <summary>
    /// Base class for all Pipeline/Transformation plugins.
    /// Handles data transformation operations (compression, encryption, encoding, etc.).
    ///
    /// Pipeline plugins transform data as it flows through the system:
    /// - On Write: Apply transformation (compress, encrypt, etc.)
    /// - On Read: Reverse transformation (decompress, decrypt, etc.)
    ///
    /// Examples: GZip compression, AES encryption, Base64 encoding, etc.
    ///
    /// Plugins inheriting from this only need to implement:
    /// 1. TransformType property (e.g., "gzip", "aes")
    /// 2. ApplyTransformAsync() - Forward transformation logic
    /// 3. ReverseTransformAsync() - Reverse transformation logic
    ///
    /// Everything else (handshake, message handling, capability registration) is done automatically.
    /// </summary>
    public abstract class PipelinePluginBase : PluginBase
    {
        // =========================================================================
        // CONSTRUCTOR
        // =========================================================================

        /// <summary>
        /// Constructs a pipeline plugin with the specified metadata.
        /// Automatically sets category to Pipeline.
        /// </summary>
        /// <param name="id">Unique plugin identifier (e.g., "DataWarehouse.Pipeline.GZip").</param>
        /// <param name="name">Human-readable name (e.g., "GZip Compression").</param>
        /// <param name="version">Plugin version.</param>
        protected PipelinePluginBase(string id, string name, Version version)
            : base(id, name, version, PluginCategory.Pipeline)
        {
        }

        // =========================================================================
        // ABSTRACT MEMBERS - Plugin must implement these
        // =========================================================================

        /// <summary>
        /// The type of transformation this plugin performs.
        /// Used to generate capability IDs like "transform.{type}.apply" and "transform.{type}.reverse".
        ///
        /// Examples:
        /// - "gzip" for GZip compression
        /// - "aes" for AES encryption
        /// - "zstd" for Zstandard compression
        /// - "base64" for Base64 encoding
        ///
        /// Should be lowercase, no spaces, alphanumeric only.
        /// </summary>
        protected abstract string TransformType { get; }

        /// <summary>
        /// Applies the transformation to input data.
        /// This is the FORWARD transformation (e.g., compress, encrypt).
        ///
        /// Plugin implements ONLY the algorithm, not the plumbing.
        /// Base class handles parameter extraction, error handling, and message routing.
        ///
        /// Examples:
        /// - Compression: Take raw bytes, return compressed bytes
        /// - Encryption: Take plaintext bytes, return ciphertext bytes
        /// - Encoding: Take binary data, return encoded bytes
        /// </summary>
        /// <param name="input">Raw input data to transform.</param>
        /// <param name="args">Transformation arguments (e.g., compression level, encryption key).</param>
        /// <returns>Transformed data.</returns>
        protected abstract Task<byte[]> ApplyTransformAsync(byte[] input, Dictionary<string, object> args);

        /// <summary>
        /// Reverses the transformation on input data.
        /// This is the REVERSE transformation (e.g., decompress, decrypt).
        ///
        /// Should be the exact inverse of ApplyTransformAsync().
        /// ApplyTransformAsync(ReverseTransformAsync(data)) should equal original data.
        ///
        /// Examples:
        /// - Decompression: Take compressed bytes, return raw bytes
        /// - Decryption: Take ciphertext bytes, return plaintext bytes
        /// - Decoding: Take encoded bytes, return binary data
        /// </summary>
        /// <param name="input">Transformed data to reverse.</param>
        /// <param name="args">Transformation arguments (must match those used in Apply).</param>
        /// <returns>Original data.</returns>
        protected abstract Task<byte[]> ReverseTransformAsync(byte[] input, Dictionary<string, object> args);

        // =========================================================================
        // VIRTUAL MEMBERS - Plugin can override if needed
        // =========================================================================

        /// <summary>
        /// Plugin-specific initialization (optional override).
        /// Called during handshake after base initialization.
        /// Use this for custom setup like loading config, initializing algorithms, etc.
        /// </summary>
        /// <param name="context">Kernel context.</param>
        protected virtual void InitializePipeline(IKernelContext context)
        {
            // Default: no additional initialization needed
        }

        // =========================================================================
        // CAPABILITIES - Automatically declared by base class
        // =========================================================================

        /// <summary>
        /// Declares the capabilities this pipeline plugin provides.
        /// Automatically generates two capabilities:
        /// 1. "{transform.{type}.apply}" - Forward transformation
        /// 2. "transform.{type}.reverse" - Reverse transformation
        ///
        /// No need to override unless you want custom capabilities.
        /// </summary>
        protected override PluginCapabilityDescriptor[] Capabilities => new[]
        {
            // Forward transformation capability
            new PluginCapabilityDescriptor
            {
                CapabilityId = $"transform.{TransformType}.apply",
                DisplayName = $"{PluginName} (Apply)",
                Description = $"Apply {TransformType} transformation to data",
                Category = CapabilityCategory.Transform,
                RequiredPermission = Security.Permission.Read,
                RequiresApproval = false,
                ParameterSchemaJson = BuildApplySchema(),
                Tags = new List<string> { "transform", TransformType, "forward" }
            },

            // Reverse transformation capability
            new PluginCapabilityDescriptor
            {
                CapabilityId = $"transform.{TransformType}.reverse",
                DisplayName = $"{PluginName} (Reverse)",
                Description = $"Reverse {TransformType} transformation",
                Category = CapabilityCategory.Transform,
                RequiredPermission = Security.Permission.Read,
                RequiresApproval = false,
                ParameterSchemaJson = BuildReverseSchema(),
                Tags = new List<string> { "transform", TransformType, "reverse" }
            }
        };

        // =========================================================================
        // INITIALIZATION - Automatically registers capability handlers
        // =========================================================================

        /// <summary>
        /// Initializes the pipeline plugin (IMPLEMENTED ONCE for all pipeline plugins).
        /// Automatically registers handlers for apply and reverse transformations.
        /// Plugins don't override this - they implement InitializePipeline() instead.
        /// </summary>
        /// <param name="context">Kernel context.</param>
        protected override void InitializeInternal(IKernelContext context)
        {
            // Register forward transformation handler
            RegisterCapability($"transform.{TransformType}.apply", async (parameters) =>
            {
                // Extract input data (supports byte[], Stream, or base64 string)
                var input = ExtractInputData(parameters);

                // Extract transformation arguments
                var args = ExtractArgs(parameters);

                // Call plugin's transformation logic
                var result = await ApplyTransformAsync(input, args);

                // Return transformed data
                return result;
            });

            // Register reverse transformation handler
            RegisterCapability($"transform.{TransformType}.reverse", async (parameters) =>
            {
                // Extract input data
                var input = ExtractInputData(parameters);

                // Extract transformation arguments
                var args = ExtractArgs(parameters);

                // Call plugin's reverse logic
                var result = await ReverseTransformAsync(input, args);

                // Return original data
                return result;
            });

            // Call plugin-specific initialization if needed
            InitializePipeline(context);
        }

        // =========================================================================
        // HELPER METHODS - Common logic for all pipeline plugins
        // =========================================================================

        /// <summary>
        /// Extracts input data from parameters.
        /// Handles multiple input formats:
        /// - byte[] array (direct)
        /// - Stream (reads to byte array)
        /// - string (treats as base64 encoded)
        /// </summary>
        /// <param name="parameters">Message parameters.</param>
        /// <returns>Input data as byte array.</returns>
        private byte[] ExtractInputData(Dictionary<string, object> parameters)
        {
            if (!parameters.ContainsKey("input"))
            {
                throw new ArgumentException("Missing required parameter: input");
            }

            var input = parameters["input"];

            // Handle different input types
            return input switch
            {
                byte[] bytes => bytes,
                Stream stream => ReadStreamToBytes(stream),
                string base64 => Convert.FromBase64String(base64),
                _ => throw new ArgumentException($"Unsupported input type: {input.GetType().Name}. Expected byte[], Stream, or base64 string.")
            };
        }

        /// <summary>
        /// Extracts transformation arguments from parameters.
        /// Returns empty dictionary if no args provided.
        /// </summary>
        /// <param name="parameters">Message parameters.</param>
        /// <returns>Transformation arguments.</returns>
        private Dictionary<string, object> ExtractArgs(Dictionary<string, object> parameters)
        {
            if (parameters.ContainsKey("args") && parameters["args"] is Dictionary<string, object> args)
            {
                return args;
            }

            return new Dictionary<string, object>();
        }

        /// <summary>
        /// Reads a stream into a byte array.
        /// </summary>
        /// <param name="stream">Input stream.</param>
        /// <returns>Stream contents as byte array.</returns>
        private byte[] ReadStreamToBytes(Stream stream)
        {
            if (stream is MemoryStream ms)
            {
                return ms.ToArray();
            }

            using var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            return memoryStream.ToArray();
        }

        /// <summary>
        /// Builds JSON schema for forward transformation parameters.
        /// Override to provide custom schema with transformation-specific parameters.
        /// </summary>
        /// <returns>JSON schema string.</returns>
        protected virtual string BuildApplySchema()
        {
            return """
            {
                "type": "object",
                "properties": {
                    "input": {
                        "description": "Input data to transform (byte array, stream, or base64 string)",
                        "oneOf": [
                            { "type": "string", "format": "byte" },
                            { "type": "string", "contentEncoding": "base64" },
                            { "type": "object" }
                        ]
                    },
                    "args": {
                        "type": "object",
                        "description": "Transformation-specific arguments",
                        "additionalProperties": true
                    }
                },
                "required": ["input"]
            }
            """;
        }

        /// <summary>
        /// Builds JSON schema for reverse transformation parameters.
        /// Override to provide custom schema.
        /// </summary>
        /// <returns>JSON schema string.</returns>
        protected virtual string BuildReverseSchema()
        {
            return BuildApplySchema(); // Same schema as forward by default
        }
    }
}
