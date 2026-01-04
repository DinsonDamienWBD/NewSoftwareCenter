using DataWarehouse.SDK.Contracts;
using DataWarehouse.SDK.Governance;
using DataWarehouse.SDK.Primitives;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace DataWarehouse.Plugins.Features.AI.Modules
{
    // ==========================================
    // 1. SECURITY & COMPLIANCE
    // ==========================================

    public partial class PiiDetectorModule : ISentinelModule
    {
        public string ModuleId => "PiiDetector";

        private static readonly Regex SecretPattern = MySecretPatternRegex();

        public async Task<GovernanceJudgment> AnalyzeAsync(SentinelContext context, IKernelContext kernel)
        {
            var result = new GovernanceJudgment();
            if (context.Trigger != TriggerType.OnWrite || context.DataStream == null) return result;
            if (context.Metadata.SizeBytes > 5 * 1024 * 1024) return result;

            using var reader = new StreamReader(context.DataStream, leaveOpen: true);
            var content = await reader.ReadToEndAsync();
            context.DataStream.Position = 0;

            if (SecretPattern.IsMatch(content))
            {
                result.InterventionRequired = true;
                result.EnforcePipeline = new PipelineConfig
                {
                    TransformationOrder = ["Compression", "Encryption"],
                    EnableEncryption = true,
                    EnableCompression = true,
                    KeyId = "AUTO-REMEDIATION-MASTER"
                };
                result.AddTags.Add("Governance:AutoEncrypted");
                result.Alert = new GovernanceAlert { Code = "PII_SECRET", Message = "Plaintext secret detected.", Severity = AlertSeverity.Warning };
            }
            return result;
        }

        [SentinelSkill("ScanTextForSecrets", "Scans text for API keys and passwords.", "Security")]
        public static GovernanceResult ScanText([SentinelParameter("Text to scan")] string text)
        {
            return new GovernanceResult
            {
                RiskDetected = SecretPattern.IsMatch(text),
                RiskType = "PII",
                RecommendedAction = "Encrypt"
            };
        }

        [GeneratedRegex(@"(password|secret|apikey|access_key)\s*[:=]\s*['""]?([a-zA-Z0-9@#$%^&+=]{8,})['""]?", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
        private static partial Regex MySecretPatternRegex();
    }

    public class GdprComplianceModule : ISentinelModule
    {
        public string ModuleId => "GdprCompliance";

        public async Task<GovernanceJudgment> AnalyzeAsync(SentinelContext context, IKernelContext kernel)
        {
            var result = new GovernanceJudgment();
            if (context.Metadata.ContainerId.Equals("public", StringComparison.OrdinalIgnoreCase))
            {
                if (context.Metadata.Tags.ContainsKey("UserData") || context.Metadata.Tags.ContainsKey("PII"))
                {
                    result.InterventionRequired = true;
                    result.BlockOperation = true;
                    result.Alert = new GovernanceAlert
                    {
                        Code = "GDPR_VIOLATION",
                        Message = "GDPR: User Data cannot be stored in Public container.",
                        Severity = AlertSeverity.Critical
                    };
                }
            }
            return await Task.FromResult(result);
        }
    }

    public class SteganographyDetectorModule : ISentinelModule
    {
        public string ModuleId => "SteganographyDetector";

        // Magic Numbers
        private static readonly byte[] JPEG_MAGIC = [0xFF, 0xD8, 0xFF];
        private static readonly byte[] PNG_MAGIC = [0x89, 0x50, 0x4E, 0x47];
        private static readonly byte[] MZ_HEADER = [0x4D, 0x5A]; // Windows Executable

        public async Task<GovernanceJudgment> AnalyzeAsync(SentinelContext context, IKernelContext kernel)
        {
            var result = new GovernanceJudgment();
            if (context.Trigger != TriggerType.OnWrite || context.DataStream == null) return result;

            // Only scan likely image formats
            bool isJpg = context.Metadata.BlobUri.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase);
            bool isPng = context.Metadata.BlobUri.EndsWith(".png", StringComparison.OrdinalIgnoreCase);

            if (isJpg || isPng)
            {
                var header = new byte[16];
                int read = await context.DataStream.ReadAsync(header.AsMemory(0, 16));
                context.DataStream.Position = 0; // Reset immediately

                if (read < 4) return result;

                // 1. Verify Header Matches Extension
                bool headerMismatch = false;
                if (isJpg && !StartsWith(header, JPEG_MAGIC)) headerMismatch = true;
                if (isPng && !StartsWith(header, PNG_MAGIC)) headerMismatch = true;

                if (headerMismatch)
                {
                    result.InterventionRequired = true;
                    result.AddTags.Add("Risk:ExtensionMismatch");
                    result.Alert = new GovernanceAlert { Code = "STEGO_EXTENSION", Message = "File extension does not match binary header.", Severity = AlertSeverity.Error };
                }

                // 2. Check for Executable code hiding (MZ header anomaly at start)
                // Note: Valid images shouldn't start with MZ
                if (StartsWith(header, MZ_HEADER))
                {
                    result.InterventionRequired = true;
                    result.BlockOperation = true;
                    result.Alert = new GovernanceAlert { Code = "STEGO_EXE", Message = "Executable binary header detected in image file.", Severity = AlertSeverity.Critical };
                }
            }
            return result;
        }

        private static bool StartsWith(byte[] data, byte[] magic)
        {
            if (data.Length < magic.Length) return false;
            for (int i = 0; i < magic.Length; i++)
                if (data[i] != magic[i]) return false;
            return true;
        }
    }

    // ==========================================
    // 2. DATA INTEGRITY & OPTIMIZATION
    // ==========================================

    public class IntegrityCheckModule : ISentinelModule
    {
        public string ModuleId => "IntegrityChecker";

        public async Task<GovernanceJudgment> AnalyzeAsync(SentinelContext context, IKernelContext kernel)
        {
            var result = new GovernanceJudgment();

            // Run on Read or Scheduled Scan
            if ((context.Trigger == TriggerType.OnRead || context.Trigger == TriggerType.OnSchedule)
                && context.DataStream != null
                && !string.IsNullOrEmpty(context.Metadata.Checksum))
            {
                using var sha = SHA256.Create();
                var hash = Convert.ToHexString(sha.ComputeHash(context.DataStream));
                context.DataStream.Position = 0;

                if (hash != context.Metadata.Checksum)
                {
                    result.InterventionRequired = true;
                    result.BlockOperation = true; // Stop user from consuming corrupt data
                    result.AddTags.Add("Status:Corrupt");

                    // Attempt to locate a replica ID from metadata tags
                    // Convention: "Replica:Node1", "Replica:Node2"
                    var replicaTag = context.Metadata.Tags.Keys.FirstOrDefault(k => k.StartsWith("Replica:"));
                    if (replicaTag != null)
                    {
                        // Extract "Node1" from "Replica:Node1"
                        result.HealWithReplicaId = replicaTag[8..];
                    }

                    result.Alert = new GovernanceAlert
                    {
                        Code = "BIT_ROT",
                        Message = $"Integrity Failure. Hash mismatch. {(result.HealWithReplicaId != null ? "Requesting Healing." : "No replica found.")}",
                        Severity = AlertSeverity.Critical
                    };
                }
                else
                {
                    // Verified
                    result.InterventionRequired = true;
                    result.AddTags.Add($"Verified:{DateTime.UtcNow:yyyy-MM-dd}");
                }
            }
            return await Task.FromResult(result);
        }
    }

    public class DeduplicationAdvisorModule : ISentinelModule
    {
        public string ModuleId => "DeduplicationAdvisor";

        public async Task<GovernanceJudgment> AnalyzeAsync(SentinelContext context, IKernelContext kernel)
        {
            var result = new GovernanceJudgment();

            // Only check OnWrite for new files with a valid checksum
            if (context.Trigger == TriggerType.OnWrite && !string.IsNullOrEmpty(context.Metadata.Checksum))
            {
                var index = kernel.GetPlugin<IMetadataIndex>();

                // [FIX] Check if the index supports the query method directly
                // (In the V9 SDK, IMetadataIndex includes ExecuteQueryAsync)
                if (index != null)
                {
                    var query = new CompositeQuery
                    {
                        Filters =
                        [
                            new QueryFilter { Field = "Checksum", Operator = "==", Value = context.Metadata.Checksum }
                        ]
                    };

                    try
                    {
                        // Execute Query directly on IMetadataIndex
                        var matches = await index.ExecuteQueryAsync(query, 1);

                        if (matches.Length > 0)
                        {
                            result.InterventionRequired = true;
                            result.AddTags.Add($"Dedupe:DuplicateOf={matches[0]}");

                            if (context.Metadata.SizeBytes > 50 * 1024 * 1024)
                            {
                                result.Alert = new GovernanceAlert
                                {
                                    Code = "DUPLICATE_FOUND",
                                    Message = $"Content exists in {matches[0]}. Marked for dedupe.",
                                    Severity = AlertSeverity.Info
                                };
                            }
                        }
                    }
                    catch
                    {
                        // Index might not support this query type yet, ignore
                    }
                }
            }
            return result;
        }
    }

    public class CompressionAdvisorModule : ISentinelModule
    {
        public string ModuleId => "CompressionAdvisor";

        public async Task<GovernanceJudgment> AnalyzeAsync(SentinelContext context, IKernelContext kernel)
        {
            var result = new GovernanceJudgment();

            // [FIX CS1061] Use CompressionProviderId instead of CompressionAlgo
            bool isCompressed = !string.IsNullOrEmpty(context.Metadata.Pipeline.CompressionProviderId)
                                && context.Metadata.Pipeline.CompressionProviderId != "None";

            if (context.Trigger == TriggerType.OnWrite
                && !isCompressed
                && (context.Metadata.BlobUri.EndsWith(".json") || context.Metadata.BlobUri.EndsWith(".xml") || context.Metadata.BlobUri.EndsWith(".csv")))
            {
                // Rule: If > 10MB and text-based, FORCE compression
                if (context.Metadata.SizeBytes > 10 * 1024 * 1024)
                {
                    result.InterventionRequired = true;

                    // Clone existing order and inject Compression at the start
                    var newOrder = new List<string>(context.Metadata.Pipeline.TransformationOrder);
                    if (!newOrder.Contains("Compression"))
                    {
                        newOrder.Insert(0, "Compression");
                    }

                    result.EnforcePipeline = new PipelineConfig
                    {
                        TransformationOrder = newOrder,
                        EnableEncryption = context.Metadata.Pipeline.EnableEncryption,
                        CryptoProviderId = context.Metadata.Pipeline.CryptoProviderId,
                        KeyId = context.Metadata.Pipeline.KeyId,

                        // Enforce GZip
                        EnableCompression = true,
                        CompressionProviderId = "standard-gzip"
                    };

                    result.AddTags.Add("Optimization:AutoCompressed");
                    result.Alert = new GovernanceAlert
                    {
                        Code = "AUTO_COMPRESS",
                        Message = "Large text file detected. Auto-Compression enabled to save space.",
                        Severity = AlertSeverity.Info
                    };
                }
            }
            return await Task.FromResult(result);
        }
    }

    // ==========================================
    // 3. ORGANIZATION & CONTEXT
    // ==========================================

    public class AutoTaggingModule : ISentinelModule
    {
        public string ModuleId => "AutoTagging";

        public async Task<GovernanceJudgment> AnalyzeAsync(SentinelContext context, IKernelContext kernel)
        {
            var result = new GovernanceJudgment();
            var name = context.Metadata.BlobUri.ToLower();

            if (name.Contains("receipt") || name.Contains("invoice"))
            {
                result.InterventionRequired = true;
                result.AddTags.Add("Type:Financial");
            }
            if (name.Contains("contract") || name.Contains("agreement"))
            {
                result.InterventionRequired = true;
                result.AddTags.Add("Type:Legal");
            }
            return await Task.FromResult(result);
        }
    }

    public partial class RelationshipMapperModule : ISentinelModule
    {
        public string ModuleId => "RelationshipMapper";
        private static readonly Regex ProjectRef = MyProjectRefRegex();

        public async Task<GovernanceJudgment> AnalyzeAsync(SentinelContext context, IKernelContext kernel)
        {
            var result = new GovernanceJudgment();
            // Shallow scan for small text files
            if (context.DataStream != null && context.Metadata.SizeBytes < 500 * 1024)
            {
                using var reader = new StreamReader(context.DataStream, leaveOpen: true);
                var content = await reader.ReadToEndAsync();
                context.DataStream.Position = 0;

                var matches = ProjectRef.Matches(content);
                if (matches.Count > 0)
                {
                    result.InterventionRequired = true;
                    foreach (Match m in matches)
                    {
                        var projId = m.Groups[1].Value.ToUpper();
                        result.AddTags.Add($"Rel:Project-{projId}");
                        // Also update metadata property for fast SQL lookup
                        if (!result.UpdateProperties.ContainsKey("LinkedProject"))
                            result.UpdateProperties["LinkedProject"] = projId;
                    }
                }
            }
            return result;
        }

        [GeneratedRegex(@"Project\s?[-_]?\s?([A-Z0-9]{3,})", RegexOptions.IgnoreCase, "en-US")]
        private static partial Regex MyProjectRefRegex();
    }

    public class SentimentAnalysisModule : ISentinelModule
    {
        public string ModuleId => "SentimentAnalysis";
        private static readonly string[] HostileWords = ["stupid", "idiot", "hate", "useless", "incompetent"];

        public async Task<GovernanceJudgment> AnalyzeAsync(SentinelContext context, IKernelContext kernel)
        {
            var result = new GovernanceJudgment();
            if (context.Metadata.BlobUri.EndsWith(".eml") || context.Metadata.BlobUri.EndsWith(".txt"))
            {
                if (context.DataStream != null)
                {
                    using var reader = new StreamReader(context.DataStream, leaveOpen: true);
                    var content = await reader.ReadToEndAsync();
                    context.DataStream.Position = 0;

                    int hostilityScore = HostileWords.Count(w => content.Contains(w, StringComparison.OrdinalIgnoreCase));
                    if (hostilityScore > 0)
                    {
                        result.InterventionRequired = true;
                        result.AddTags.Add("Sentiment:Negative");
                        if (hostilityScore >= 2)
                        {
                            result.Alert = new GovernanceAlert
                            {
                                Code = "HR_FLAG",
                                Message = "Hostile content detected. Flagged for review.",
                                Severity = AlertSeverity.Warning
                            };
                        }
                    }
                }
            }
            return result;
        }
    }
}