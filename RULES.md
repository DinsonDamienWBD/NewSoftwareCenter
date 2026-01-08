# DataWarehouse Development Rules & Standards

**God-Tier Production-Ready Code Standards**

Last Updated: 2025-01-07

---

## 🎯 Core Philosophy

**Quality Level:** God-Tier (Superior to Diamond Tier)
- **Production Readiness:** Acceptable for deployment at Google, Microsoft, Amazon
- **Industry Comparison:** Superior to existing industry heavyweights
- **No Compromises:** Zero placeholders, simulations, or simplifications
- **AI-Native:** Every component designed for AI integration from the ground up

---

## 📋 Absolute Requirements

### 1. Production-Ready Implementation

**RULE:** Every line of code must be production-ready.

**This means:**
- ✅ Full error handling with try-catch blocks
- ✅ Input validation for all public methods
- ✅ Null checks for all nullable parameters
- ✅ Thread-safe operations where concurrency is possible
- ✅ Resource disposal (IDisposable pattern where applicable)
- ✅ Logging of errors and warnings
- ✅ Retry logic for transient failures (network, I/O)
- ✅ Timeout handling for all async operations
- ✅ Graceful degradation when dependencies unavailable

**Forbidden:**
- ❌ TODO comments
- ❌ Placeholder implementations
- ❌ Simulated responses
- ❌ Simplified logic "for now"
- ❌ Hardcoded test data in production code
- ❌ Console.WriteLine for logging (use proper logging)
- ❌ Empty catch blocks
- ❌ Magic numbers without explanation

**Example (Correct):**
```csharp
/// <summary>
/// Retrieves user by ID with full error handling.
/// </summary>
/// <param name="userId">User identifier (must be non-empty).</param>
/// <returns>User object.</returns>
/// <exception cref="ArgumentException">If userId is null or empty.</exception>
/// <exception cref="NotFoundException">If user not found.</exception>
/// <exception cref="DatabaseException">If database error occurs.</exception>
public async Task<User> GetUserAsync(string userId)
{
    if (string.IsNullOrWhiteSpace(userId))
        throw new ArgumentException("User ID cannot be empty", nameof(userId));

    try
    {
        using var connection = await _connectionPool.GetConnectionAsync();
        var user = await connection.QuerySingleOrDefaultAsync<User>(
            "SELECT * FROM users WHERE id = @UserId",
            new { UserId = userId }
        );

        if (user == null)
            throw new NotFoundException($"User '{userId}' not found");

        return user;
    }
    catch (SqlException ex)
    {
        _logger.LogError(ex, "Database error retrieving user {UserId}", userId);
        throw new DatabaseException("Failed to retrieve user", ex);
    }
    catch (TimeoutException ex)
    {
        _logger.LogWarning(ex, "Timeout retrieving user {UserId}", userId);
        throw;
    }
}
```

---

### 2. Comprehensive Documentation

**RULE:** All public APIs must have comprehensive XML documentation.

**Documentation Requirements:**
- ✅ Class-level documentation
  - Purpose and responsibility
  - Usage examples
  - Thread safety guarantees
  - Performance characteristics
- ✅ Method-level documentation
  - Summary of what it does
  - All parameters with validation rules
  - Return value description
  - All exceptions that can be thrown
  - Usage examples for complex methods
- ✅ Property-level documentation
  - Purpose and meaning
  - Valid value ranges
  - Default values
  - Thread safety
- ✅ Field-level documentation (at least for public fields)
  - Purpose
  - Thread safety if mutable
- ✅ Event-level documentation
  - When fired
  - Event arguments
  - Threading context

**Example (Correct):**
```csharp
/// <summary>
/// Optimizes execution plans for cost, speed, or reliability.
/// Compares alternative approaches and selects the best based on optimization criteria.
///
/// Thread Safety: This class is thread-safe. Multiple threads can call methods concurrently.
///
/// Performance: O(n*m) where n = number of plans, m = number of steps per plan.
///
/// Example Usage:
/// <code>
/// var optimizer = new CostOptimizer(knowledgeGraph);
/// var plans = planner.GenerateAlternatives(request);
/// var bestPlan = optimizer.SelectOptimalPlan(plans, OptimizationObjective.MinimizeCost);
/// </code>
/// </summary>
public class CostOptimizer
{
    /// <summary>
    /// Maximum cost threshold in USD for auto-optimization.
    /// Plans exceeding this cost require manual approval.
    ///
    /// Default: $10.00
    /// Thread Safety: Reads and writes are atomic.
    /// </summary>
    public decimal MaxCostThreshold { get; set; } = 10.00m;

    /// <summary>
    /// Selects the optimal execution plan based on the specified objective.
    ///
    /// This method evaluates all provided plans and returns the one that best
    /// satisfies the optimization criteria. For BalancedEfficiency, uses a
    /// weighted scoring system (40% speed, 30% cost, 30% reliability).
    /// </summary>
    /// <param name="plans">List of alternative execution plans to evaluate.
    /// Must contain at least one plan.</param>
    /// <param name="objective">Optimization objective (cost, speed, reliability, or balanced).</param>
    /// <returns>The optimal execution plan based on the criteria.</returns>
    /// <exception cref="ArgumentNullException">If plans is null.</exception>
    /// <exception cref="ArgumentException">If plans is empty.</exception>
    /// <example>
    /// <code>
    /// var plans = new List&lt;ExecutionPlan&gt; { plan1, plan2, plan3 };
    /// var optimal = optimizer.SelectOptimalPlan(plans, OptimizationObjective.MinimizeCost);
    /// Console.WriteLine($"Selected plan costs: ${optimal.EstimatedCostUsd}");
    /// </code>
    /// </example>
    public ExecutionPlan SelectOptimalPlan(List<ExecutionPlan> plans, OptimizationObjective objective)
    {
        // Implementation...
    }
}
```

---

### 3. Maximum Code Reuse

**RULE:** Never duplicate code. Always extract common functionality.

**Code Reuse Strategies:**
- ✅ Base classes for shared functionality (e.g., PluginBase, ProactiveAgent)
- ✅ Utility classes for common operations (e.g., VectorMath, StatisticalAnalyzer)
- ✅ Extension methods for repeated patterns
- ✅ Generic methods/classes for type-agnostic operations
- ✅ Composition over inheritance where appropriate
- ✅ Dependency injection for pluggable components

**Example (Plugin Architecture):**
```csharp
// CORRECT: Base class handles common logic once
public abstract class PluginBase : IPlugin
{
    // Common handshake protocol implemented once
    public virtual async Task<HandshakeResponse> OnHandshakeAsync(HandshakeRequest request)
    {
        // ... handshake logic used by ALL plugins
    }

    // Plugins only implement their specific logic
    protected abstract void InitializeInternal(IKernelContext context);
}

// Plugins inherit and only implement unique logic
public class GZipCompressionPlugin : PipelinePluginBase
{
    // Only ~60 lines of unique logic
    // All boilerplate handled by base classes
}
```

**Anti-Pattern (Forbidden):**
```csharp
// WRONG: Duplicating handshake logic in every plugin
public class Plugin1 : IPlugin
{
    public async Task<HandshakeResponse> OnHandshakeAsync(...)
    {
        // 50 lines of handshake logic
    }
}

public class Plugin2 : IPlugin
{
    public async Task<HandshakeResponse> OnHandshakeAsync(...)
    {
        // Same 50 lines duplicated!
    }
}
```

---

### 4. Message-Based Architecture

**RULE:** Use message/command pattern for all plugin communication.

**Architecture Requirements:**
- ✅ No direct function calls between components
- ✅ All communication via messages (PluginMessage class)
- ✅ Async message handling (OnMessageAsync)
- ✅ Message types as strings (e.g., "InvokeCapability", "HealthCheck")
- ✅ Payload as dictionary for flexibility
- ✅ Standardized response format (MessageResponse)

**Message Flow:**
```
Kernel → PluginMessage → Plugin.OnMessageAsync() → MessageResponse → Kernel
```

**Example (Correct):**
```csharp
// CORRECT: Message-based communication
var message = new PluginMessage
{
    MessageType = "InvokeCapability",
    Payload = new Dictionary<string, object>
    {
        ["capabilityId"] = "transform.gzip.apply",
        ["parameters"] = new Dictionary<string, object>
        {
            ["data"] = dataBytes
        }
    }
};

var response = await plugin.OnMessageAsync(message);
if (response.Success)
{
    var result = response.Data;
}
```

**Anti-Pattern (Forbidden):**
```csharp
// WRONG: Direct function call
var result = plugin.CompressData(dataBytes); // Too tightly coupled!
```

---

### 5. Standardized Plugin Architecture

**RULE:** All plugins must follow the standardized structure.

**Directory Structure:**
```
Plugins/
  DataWarehouse.Plugins.{Category}.{Name}/
    Bootstrapper/
      Init.cs              ← Plugin entry point (inherits category base)
    Engine/
      {Name}Engine.cs      ← Core logic (stateless, pure functions)
    Service/
      {Name}Service.cs     ← Optional: stateful services
    Models/
      {Name}Models.cs      ← Optional: data models
```

**Plugin Structure Requirements:**
- ✅ Bootstrapper/Init.cs: Inherits from category-specific base (e.g., PipelinePluginBase)
- ✅ Engine/{Name}Engine.cs: Contains core stateless logic
- ✅ Metadata in constructor (not functions)
- ✅ Only public methods: constructor, InitializePipeline (if needed)
- ✅ Everything else private or protected
- ✅ AI metadata: SemanticDescription, SemanticTags, PerformanceProfile, CapabilityRelationships, UsageExamples

**Example (Correct Structure):**
```csharp
// File: Bootstrapper/Init.cs
namespace DataWarehouse.Plugins.Compression.GZip.Bootstrapper
{
    /// <summary>
    /// GZip compression plugin.
    /// Provides fast lossless compression using the GZip algorithm (RFC 1952).
    /// </summary>
    public class GZipCompressionPlugin : PipelinePluginBase
    {
        /// <summary>
        /// Constructs the GZip compression plugin.
        /// </summary>
        public GZipCompressionPlugin()
            : base(
                id: "DataWarehouse.Pipeline.GZip",
                name: "GZip Compression",
                version: new Version(2, 0, 0))
        {
        }

        protected override string TransformType => "gzip";

        protected override async Task<byte[]> ApplyTransformAsync(byte[] input, Dictionary<string, object> args)
        {
            return await GZipEngine.CompressAsync(input, args);
        }

        protected override async Task<byte[]> ReverseTransformAsync(byte[] input, Dictionary<string, object> args)
        {
            return await GZipEngine.DecompressAsync(input, args);
        }

        // AI Metadata
        protected override string SemanticDescription =>
            "Fast compression using GZip. Best for text, logs, JSON. 3-4x ratio.";

        protected override string[] SemanticTags => new[]
        {
            "compression", "gzip", "fast", "lossless", "standard"
        };

        protected override PerformanceCharacteristics PerformanceProfile => new()
        {
            AverageLatencyMs = 50,
            ThroughputBytesPerSecond = 20 * 1024 * 1024,
            ReliabilityScore = 0.99
        };

        // ... CapabilityRelationships, UsageExamples
    }
}

// File: Engine/GZipEngine.cs
namespace DataWarehouse.Plugins.Compression.GZip.Engine
{
    /// <summary>
    /// Core GZip compression engine.
    /// All methods are stateless and thread-safe.
    /// </summary>
    internal static class GZipEngine
    {
        /// <summary>
        /// Compresses data using GZip algorithm.
        /// </summary>
        /// <param name="input">Uncompressed data.</param>
        /// <param name="args">Compression options (level, buffer size).</param>
        /// <returns>Compressed data.</returns>
        public static async Task<byte[]> CompressAsync(byte[] input, Dictionary<string, object> args)
        {
            // Full implementation with error handling
            if (input == null || input.Length == 0)
                throw new ArgumentException("Input cannot be empty", nameof(input));

            var level = GetCompressionLevel(args);

            using var outputStream = new MemoryStream();
            using (var gzipStream = new GZipStream(outputStream, level, leaveOpen: true))
            {
                await gzipStream.WriteAsync(input, 0, input.Length);
            }

            return outputStream.ToArray();
        }

        private static CompressionLevel GetCompressionLevel(Dictionary<string, object> args)
        {
            if (args.TryGetValue("level", out var levelObj) && levelObj is string levelStr)
            {
                return levelStr.ToLowerInvariant() switch
                {
                    "fastest" => CompressionLevel.Fastest,
                    "optimal" => CompressionLevel.Optimal,
                    "nocompression" => CompressionLevel.NoCompression,
                    _ => CompressionLevel.Fastest
                };
            }
            return CompressionLevel.Fastest;
        }
    }
}
```

---

### 6. AI-Native Integration

**RULE:** Every component must be designed for AI integration from the start.

**AI-Native Requirements:**
- ✅ Semantic descriptions in natural language
- ✅ Semantic tags for categorization
- ✅ Performance profiles for optimization
- ✅ Capability relationships for planning
- ✅ Usage examples for learning
- ✅ Event emission for observability
- ✅ Standardized parameter schemas (JSON Schema)

**AI Metadata Template:**
```csharp
// Every plugin must provide this metadata

/// <summary>Semantic description for AI understanding.</summary>
protected override string SemanticDescription =>
    "Clear natural language explanation of what this does. " +
    "Include key characteristics, use cases, and performance traits.";

/// <summary>Semantic tags for AI categorization.</summary>
protected override string[] SemanticTags => new[]
{
    "category-tag",          // What domain? (compression, storage, etc.)
    "technology-tag",        // What algorithm? (gzip, aes, etc.)
    "characteristic-tag",    // What properties? (fast, secure, etc.)
    "use-case-tag"          // When to use? (logs, backups, etc.)
};

/// <summary>Performance profile for AI optimization.</summary>
protected override PerformanceCharacteristics PerformanceProfile => new()
{
    AverageLatencyMs = 50,                           // Based on benchmarks
    ThroughputBytesPerSecond = 20 * 1024 * 1024,    // 20 MB/s
    MemoryUsageBytes = 12 * 1024 * 1024,            // 12 MB
    CpuUsagePercent = 70,                            // 70% of one core
    CostPerOperationUsd = 0,                         // Free (CPU-only)
    LinearScaling = true,                            // O(n) complexity
    MinimumEfficientSizeBytes = 1024,               // Efficient for >1KB
    ReliabilityScore = 0.99                          // 99% reliable
};

/// <summary>Capability relationships for AI planning.</summary>
protected override CapabilityRelationship[] CapabilityRelationships => new[]
{
    new CapabilityRelationship(
        relationType: "flows_into",
        targetCapabilityId: "transform.aes.apply",
        description: "Compressed data should be encrypted",
        strength: 0.8
    )
};

/// <summary>Usage examples for AI learning.</summary>
protected override PluginUsageExample[] UsageExamples => new[]
{
    new PluginUsageExample(
        title: "Compress large log file",
        description: "Compress 100MB text log to reduce storage by 70%",
        capabilityId: "transform.gzip.apply",
        inputDescription: "100MB plain text log file",
        exampleParameters: new Dictionary<string, object> { ["level"] = "fastest" },
        expectedOutput: "Compressed data (~30MB, 70% reduction)",
        tags: new[] { "logs", "storage-optimization" }
    )
};
```

---

### 7. Error Handling & Resilience

**RULE:** All error conditions must be handled gracefully.

**Error Handling Requirements:**
- ✅ Validate all inputs (null checks, range checks, format checks)
- ✅ Try-catch blocks for all external operations (network, I/O, DB)
- ✅ Specific exception types (not generic Exception)
- ✅ Meaningful error messages with context
- ✅ Logging of all errors with stack traces
- ✅ Retry logic for transient failures (3 attempts with exponential backoff)
- ✅ Circuit breaker pattern for repeated failures
- ✅ Fallback mechanisms where possible
- ✅ Resource cleanup in finally blocks or using statements

**Example (Correct):**
```csharp
/// <summary>
/// Calls external API with full error handling and retry logic.
/// </summary>
public async Task<ApiResponse> CallExternalApiAsync(string endpoint)
{
    if (string.IsNullOrWhiteSpace(endpoint))
        throw new ArgumentException("Endpoint cannot be empty", nameof(endpoint));

    HttpResponseMessage? response = null;
    Exception? lastException = null;

    // Retry up to 3 times with exponential backoff
    for (int attempt = 0; attempt < 3; attempt++)
    {
        try
        {
            response = await _httpClient.GetAsync(endpoint);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<ApiResponse>(content)
                    ?? throw new InvalidDataException("Response deserialization returned null");
            }

            // Don't retry on client errors (4xx)
            if ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new ApiClientException(
                    $"API returned client error {response.StatusCode}: {errorContent}");
            }

            // Retry on server errors (5xx)
            if ((int)response.StatusCode >= 500)
            {
                _logger.LogWarning(
                    "API returned server error {StatusCode}, attempt {Attempt}/3",
                    response.StatusCode, attempt + 1);

                if (attempt < 2)
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
                    continue;
                }
            }
        }
        catch (HttpRequestException ex)
        {
            lastException = ex;
            _logger.LogWarning(ex, "Network error calling API, attempt {Attempt}/3", attempt + 1);

            if (attempt < 2)
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
                continue;
            }
        }
        catch (TaskCanceledException ex)
        {
            lastException = ex;
            _logger.LogWarning(ex, "Timeout calling API, attempt {Attempt}/3", attempt + 1);

            if (attempt < 2)
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
                continue;
            }
        }
    }

    // All retries exhausted
    _logger.LogError(lastException, "Failed to call API {Endpoint} after 3 attempts", endpoint);
    throw new ApiException($"API call failed after 3 attempts", lastException);
}
```

---

### 8. Performance & Scalability

**RULE:** All code must be performant and scalable.

**Performance Requirements:**
- ✅ Async/await for all I/O operations
- ✅ Proper disposal of resources (using statements, IDisposable)
- ✅ Connection pooling for databases and HTTP clients
- ✅ Caching where appropriate (with expiration)
- ✅ Batch operations over individual operations
- ✅ Streaming for large data (don't load everything in memory)
- ✅ Pagination for large result sets
- ✅ Indexed database queries (no full table scans)
- ✅ Thread-safe concurrent operations (lock, ConcurrentDictionary, etc.)

**Example (Correct - Batch Processing):**
```csharp
// CORRECT: Batch processing for efficiency
public async Task IndexCapabilitiesBatchAsync(List<CapabilityIndexRequest> capabilities)
{
    // Generate all embeddings in one batch API call
    var texts = capabilities.Select(c => c.SemanticDescription).ToList();
    var embeddings = await _embeddingProvider.GenerateEmbeddingsBatchAsync(texts);

    // Batch insert to vector store
    var vectorEntries = capabilities
        .Select((cap, i) => new VectorEntry(cap.CapabilityId, embeddings[i], cap.Metadata))
        .ToList();

    await _vectorStore.AddBatchAsync(vectorEntries);
}
```

**Anti-Pattern (Forbidden):**
```csharp
// WRONG: Individual API calls (slow, expensive)
foreach (var capability in capabilities)
{
    var embedding = await _embeddingProvider.GenerateEmbeddingAsync(capability.SemanticDescription);
    await _vectorStore.AddAsync(capability.CapabilityId, embedding, capability.Metadata);
}
// This is 100x slower and 100x more expensive!
```

---

### 9. Testing & Validation

**RULE:** All code must be testable and include validation.

**Testing Requirements:**
- ✅ Public APIs must be unit testable
- ✅ Dependencies injected (not newed up internally)
- ✅ Interfaces for external dependencies (DB, API, file system)
- ✅ Validation of all inputs at public boundaries
- ✅ Testable error conditions
- ✅ No static dependencies that can't be mocked

**Example (Testable Design):**
```csharp
// CORRECT: Dependencies injected, fully testable
public class CostOptimizer
{
    private readonly IKnowledgeGraph _graph;

    public CostOptimizer(IKnowledgeGraph graph)
    {
        _graph = graph ?? throw new ArgumentNullException(nameof(graph));
    }

    public ExecutionPlan SelectOptimalPlan(List<ExecutionPlan> plans, OptimizationObjective objective)
    {
        if (plans == null)
            throw new ArgumentNullException(nameof(plans));
        if (plans.Count == 0)
            throw new ArgumentException("Plans cannot be empty", nameof(plans));

        // Implementation uses injected _graph
    }
}

// Unit test can inject mock graph
[Test]
public void SelectOptimalPlan_MinimizeCost_ReturnsLowestCost()
{
    var mockGraph = new Mock<IKnowledgeGraph>();
    var optimizer = new CostOptimizer(mockGraph.Object);

    var plans = new List<ExecutionPlan>
    {
        new() { EstimatedCostUsd = 1.00m },
        new() { EstimatedCostUsd = 0.50m },
        new() { EstimatedCostUsd = 2.00m }
    };

    var result = optimizer.SelectOptimalPlan(plans, OptimizationObjective.MinimizeCost);

    Assert.AreEqual(0.50m, result.EstimatedCostUsd);
}
```

---

### 10. Security & Safety

**RULE:** Security must be built in, not bolted on.

**Security Requirements:**
- ✅ Input validation (prevent SQL injection, XSS, path traversal)
- ✅ Authentication/authorization checks
- ✅ Secure credential storage (no hardcoded secrets)
- ✅ Encryption for sensitive data
- ✅ HTTPS for all network communication
- ✅ Rate limiting to prevent abuse
- ✅ Audit logging of security events
- ✅ Principle of least privilege

**Example (Secure Implementation):**
```csharp
/// <summary>
/// Validates user input to prevent SQL injection and path traversal.
/// </summary>
public void ValidateUserInput(string input)
{
    if (string.IsNullOrWhiteSpace(input))
        throw new ArgumentException("Input cannot be empty", nameof(input));

    // SQL injection patterns
    var sqlPatterns = new[] { "' OR '1'='1", "'; DROP TABLE", "' UNION SELECT" };
    if (sqlPatterns.Any(p => input.Contains(p, StringComparison.OrdinalIgnoreCase)))
    {
        _logger.LogWarning("SQL injection attempt detected: {Input}", input);
        throw new SecurityException("Potential SQL injection detected");
    }

    // Path traversal patterns
    if (input.Contains("../") || input.Contains("..\\"))
    {
        _logger.LogWarning("Path traversal attempt detected: {Input}", input);
        throw new SecurityException("Path traversal attempt detected");
    }

    // Additional validation as needed
}
```

---

## 🚀 Implementation Checklist

Before submitting any code, verify:

- [ ] ✅ All code is production-ready (no placeholders)
- [ ] ✅ Comprehensive XML documentation on all public APIs
- [ ] ✅ No code duplication (maximum reuse)
- [ ] ✅ Message-based architecture followed
- [ ] ✅ Standardized plugin structure
- [ ] ✅ AI-native metadata complete
- [ ] ✅ Full error handling with retry logic
- [ ] ✅ Performance optimized (async, batching, caching)
- [ ] ✅ Testable design (dependency injection)
- [ ] ✅ Security validated (input validation, safe defaults)
- [ ] ✅ Thread-safe where concurrent access possible
- [ ] ✅ Resources properly disposed
- [ ] ✅ Logging for errors and warnings
- [ ] ✅ Follows C# naming conventions
- [ ] ✅ No compiler warnings
- [ ] ✅ Formatted consistently

---

### 11. Task Tracking & Documentation

**RULE:** All tasks must be documented in TODO.md before implementation begins.

**Task Documentation Requirements:**
- ✅ Add tasks to TODO.md BEFORE starting work
- ✅ Break large features into atomic, trackable tasks
- ✅ Include estimated lines of code for each task
- ✅ Mark tasks with status: NOT STARTED, IN PROGRESS, COMPLETED
- ✅ Update TODO.md immediately when task status changes
- ✅ Include file paths and commit references when completed

**Rationale:**
- Ensures continuity if session ends unexpectedly
- Provides clear progress tracking
- Helps prioritize remaining work
- Documents implementation history

**Example (Correct):**
```markdown
## Phase 14: Comprehensive RAID Support

**Status:** IN PROGRESS
**Estimated:** ~1,000 lines
**Priority:** HIGH

### Tasks
- [x] Add RAID mode enum (RAID 0, 1, 5, 6, 10, 50, 60)
- [ ] Implement RAID 0 (striping) in StoragePoolManager
- [ ] Implement RAID 1 (mirroring) - enhanced from basic
- [ ] Implement RAID 5 (striping with parity)
- [ ] Implement RAID 6 (dual parity)
- [ ] Add RAID configuration validation
- [ ] Add rebuild logic for failed disks

**Commit:** Phase 14: Implement RAID 0, 1, 5 support (abc1234)
```

---

## 📚 References

- Microsoft C# Coding Conventions: https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions
- .NET API Design Guidelines: https://docs.microsoft.com/en-us/dotnet/standard/design-guidelines/
- DataWarehouse Architecture: See project README
- Plugin Development Guide: See PLUGIN_DEVELOPMENT.md

---

**Last Updated:** 2026-01-08
**Version:** 1.1
**Status:** Active - All new code must comply with these standards
