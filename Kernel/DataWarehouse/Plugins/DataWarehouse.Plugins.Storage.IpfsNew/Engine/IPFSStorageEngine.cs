using System.Text.Json;
using DataWarehouse.SDK.Contracts;
using DataWarehouse.SDK.Contracts.CategoryBases;

namespace DataWarehouse.Plugins.Storage.IpfsNew.Engine
{
    /// <summary>
    /// IPFS (InterPlanetary File System) storage provider.
    /// Provides distributed, content-addressed storage on the IPFS network.
    ///
    /// Features:
    /// - Content-addressed storage (immutable by default)
    /// - Distributed peer-to-peer network
    /// - Automatic content deduplication
    /// - Built-in integrity verification
    /// - Global content delivery
    /// - No central point of failure
    /// - Censorship resistant
    /// - Permanent storage with pinning
    ///
    /// Use cases:
    /// - Decentralized applications (dApps)
    /// - NFT metadata storage
    /// - Distributed file sharing
    /// - Archive and preservation
    /// - Content-addressed data lakes
    /// - Web3 storage
    ///
    /// Performance profile:
    /// - Read: Varies (10-500 MB/s, depends on peer availability)
    /// - Write: 5-50 MB/s (depends on network and pinning)
    /// - Latency: 100-2000ms (depends on peer discovery)
    /// - Throughput: Limited by network peers
    /// - Durability: High (replicated across peers)
    ///
    /// AI-Native metadata:
    /// - Semantic: "Store and retrieve data from the IPFS distributed network"
    /// - Cost: Low (pinning services ~$0.15/GB/month)
    /// - Reliability: High (distributed, no single point of failure)
    /// - Scalability: Very High (global network)
    /// - Security: Content-addressed (tamper-proof)
    /// </summary>
    public class IPFSStorageEngine : StorageProviderBase
    {
        private HttpClient _httpClient = new();
        private string _apiUrl = string.Empty;
        private string _gatewayUrl = string.Empty;
        private bool _pinByDefault = true;

        /// <summary>Storage type identifier</summary>
        protected override string StorageType => "ipfs";

        /// <summary>
        /// Constructs IPFS storage engine.
        /// </summary>
        public IPFSStorageEngine()
            : base("storage.ipfs", "IPFS Distributed Storage", new Version(1, 0, 0))
        {
            // AI-Native metadata
            SemanticDescription = "Store and retrieve data from the IPFS distributed network with content-addressing and automatic deduplication";

            SemanticTags = new List<string>
            {
                "storage", "ipfs", "distributed", "p2p", "decentralized",
                "content-addressed", "immutable", "web3", "blockchain",
                "nft", "dapp", "censorship-resistant"
            };

            PerformanceProfile = new PerformanceCharacteristics
            {
                AverageLatencyMs = 500.0, // Varies significantly
                ThroughputMBps = 20.0,
                CostPerExecution = 0.00015m, // ~$0.15/GB/month for pinning
                MemoryUsageMB = 15.0,
                ScalabilityRating = ScalabilityLevel.VeryHigh, // Global network
                ReliabilityRating = ReliabilityLevel.High, // Distributed
                ConcurrencySafe = true
            };

            CapabilityRelationships = new List<CapabilityRelationship>
            {
                new()
                {
                    RelatedCapabilityId = "transform.gzip.apply",
                    RelationType = RelationType.CanPipeline,
                    Description = "Compress before storing on IPFS to reduce bandwidth"
                },
                new()
                {
                    RelatedCapabilityId = "transform.aes.apply",
                    RelationType = RelationType.CanPipeline,
                    Description = "Encrypt before storing on IPFS for privacy"
                },
                new()
                {
                    RelatedCapabilityId = "storage.s3.save",
                    RelationType = RelationType.ComplementaryWith,
                    Description = "Use IPFS for immutable content, S3 for mutable data"
                }
            };

            UsageExamples = new List<PluginUsageExample>
            {
                new()
                {
                    Scenario = "Store NFT metadata on IPFS",
                    NaturalLanguageRequest = "Upload NFT metadata to IPFS and pin it",
                    ExpectedCapabilityChain = new[] { "storage.ipfs.save" },
                    EstimatedDurationMs = 1000.0,
                    EstimatedCost = 0.00015m
                },
                new()
                {
                    Scenario = "Retrieve content by CID",
                    NaturalLanguageRequest = "Load this file from IPFS using its CID",
                    ExpectedCapabilityChain = new[] { "storage.ipfs.load" },
                    EstimatedDurationMs = 500.0,
                    EstimatedCost = 0.0m
                }
            };
        }

        /// <summary>
        /// Mounts IPFS storage by connecting to IPFS daemon.
        /// </summary>
        protected override async Task MountInternalAsync(IKernelContext context)
        {
            _apiUrl = context.GetConfigValue("storage.ipfs.apiUrl") ?? "http://localhost:5001";
            _gatewayUrl = context.GetConfigValue("storage.ipfs.gatewayUrl") ?? "http://localhost:8080";
            _pinByDefault = bool.Parse(context.GetConfigValue("storage.ipfs.pinByDefault") ?? "true");

            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(5) // IPFS can be slow
            };

            // Verify IPFS daemon is running
            try
            {
                var response = await _httpClient.GetAsync($"{_apiUrl}/api/v0/version");
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception("IPFS daemon not accessible");
                }

                var versionJson = await response.Content.ReadAsStringAsync();
                context.LogInfo($"Connected to IPFS daemon: {versionJson}");
            }
            catch (Exception ex)
            {
                context.LogError($"Failed to connect to IPFS daemon: {ex.Message}");
                throw new Exception("IPFS daemon must be running (ipfs daemon)", ex);
            }
        }

        /// <summary>
        /// Unmounts IPFS storage.
        /// </summary>
        protected override async Task UnmountInternalAsync()
        {
            _httpClient?.Dispose();
            await Task.CompletedTask;
        }

        /// <summary>
        /// Reads content from IPFS using CID (Content Identifier).
        /// </summary>
        protected override async Task<byte[]> ReadBytesAsync(string key)
        {
            // Key is expected to be a CID
            var url = $"{_apiUrl}/api/v0/cat?arg={key}";

            try
            {
                var response = await _httpClient.PostAsync(url, null);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    throw new Exception($"IPFS cat failed: {error}");
                }

                return await response.Content.ReadAsByteArrayAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to read from IPFS (CID: {key}): {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Writes content to IPFS and returns CID.
        /// Note: IPFS is content-addressed, so the "key" returned is actually the CID.
        /// </summary>
        protected override async Task WriteBytesAsync(string key, byte[] data)
        {
            // For IPFS, we ignore the provided key and use the returned CID
            // The key will be stored in metadata for reference

            var url = $"{_apiUrl}/api/v0/add?pin={(_pinByDefault ? "true" : "false")}";

            try
            {
                using var content = new MultipartFormDataContent();
                content.Add(new ByteArrayContent(data), "file", "data");

                var response = await _httpClient.PostAsync(url, content);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    throw new Exception($"IPFS add failed: {error}");
                }

                var resultJson = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(resultJson);

                if (result == null || !result.ContainsKey("Hash"))
                {
                    throw new Exception("IPFS add response missing Hash field");
                }

                var cid = result["Hash"].GetString();
                // Store CID mapping in context for later retrieval
                Context?.SetMetadata($"ipfs.cid.{key}", cid ?? "");
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to write to IPFS: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Deletes (unpins) content from IPFS.
        /// Note: Content remains on network unless all nodes unpin it.
        /// </summary>
        protected override async Task DeleteBytesAsync(string key)
        {
            // Unpin the content
            var url = $"{_apiUrl}/api/v0/pin/rm?arg={key}";

            try
            {
                var response = await _httpClient.PostAsync(url, null);

                if (!response.IsSuccessStatusCode)
                {
                    // Not an error if content wasn't pinned
                    var error = await response.Content.ReadAsStringAsync();
                    if (!error.Contains("not pinned"))
                    {
                        throw new Exception($"IPFS unpin failed: {error}");
                    }
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to unpin from IPFS (CID: {key}): {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Checks if content exists on IPFS (is pinned locally).
        /// </summary>
        protected override async Task<bool> ExistsBytesAsync(string key)
        {
            var url = $"{_apiUrl}/api/v0/pin/ls?arg={key}";

            try
            {
                var response = await _httpClient.PostAsync(url, null);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Lists pinned content.
        /// Note: IPFS doesn't support prefix-based listing like traditional storage.
        /// </summary>
        protected override async Task<List<string>> ListKeysAsync(string prefix)
        {
            var url = $"{_apiUrl}/api/v0/pin/ls?type=recursive";

            try
            {
                var response = await _httpClient.PostAsync(url, null);

                if (!response.IsSuccessStatusCode)
                {
                    return new List<string>();
                }

                var resultJson = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(resultJson);

                if (result == null || !result.ContainsKey("Keys"))
                {
                    return new List<string>();
                }

                var keys = new List<string>();
                var keysObj = result["Keys"];

                if (keysObj.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in keysObj.EnumerateObject())
                    {
                        keys.Add(property.Name);
                    }
                }

                // Filter by prefix if provided (though IPFS CIDs don't have meaningful prefixes)
                if (!string.IsNullOrEmpty(prefix))
                {
                    keys = keys.Where(k => k.StartsWith(prefix)).ToList();
                }

                return keys;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to list IPFS pins: {ex.Message}", ex);
            }
        }
    }
}
