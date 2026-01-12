# DataWarehouse SDK Documentation

## Overview

The DataWarehouse SDK is designed to be **AI-native from the ground up**, providing a future-proof, extensible plugin architecture for data warehouse operations. The SDK supports intelligent automation, LLM integration, and autonomous decision-making.

## Architecture Philosophy

### 1. Abstract Base Classes Over Interfaces
All plugins should inherit from abstract base classes rather than implementing interfaces directly. This provides:
- **Default implementations** for common functionality
- **Reduced boilerplate code** for plugin developers
- **Consistent behavior** across all plugins
- **Future-proof design** - new features can be added without breaking existing plugins

### 2. AI-Native Design Principles
- **Self-describing**: All plugins expose capabilities, metadata, and schemas for AI agents
- **Composable**: Plugins can invoke each other's capabilities for complex workflows
- **Context-aware**: Execution contexts provide rich information for intelligent decisions
- **Approval-driven**: AI agents can request human approval for critical operations
- **Semantic-friendly**: Natural language descriptions throughout for LLM processing

### 3. Minimal Plugin Implementation
Plugins only need to implement core business logic. Common functionality is handled by base classes:
- Handshake protocol
- Message handling
- Capability discovery
- Dependency resolution
- Metadata reporting

---

## Abstract Base Classes

### `PluginBase`
The foundation for all plugins. Provides default implementations of the `IPlugin` interface.

**What you must implement:**
```csharp
public abstract Guid Id { get; }
public abstract PluginCategory Category { get; }
public abstract string Name { get; }
```

**What's provided for you:**
- Default `Version` property (returns "1.0.0")
- `OnHandshakeAsync()` - Automatic plugin registration
- `OnMessageAsync()` - Message handling (no-op by default)
- Metadata generation for AI agents

**What you can override:**
- `GetCapabilities()` - Describe what your plugin can do for AI agents
- `GetDependencies()` - Declare required plugins
- `GetMetadata()` - Add custom AI-friendly metadata

**Example:**
```csharp
public class MyCustomPlugin : PluginBase
{
    public override Guid Id => Guid.Parse("...");
    public override PluginCategory Category => PluginCategory.SecurityProvider;
    public override string Name => "My Custom Plugin";
    public override string Version => "2.0.0";

    protected override Dictionary<string, object> GetMetadata()
    {
        var metadata = base.GetMetadata();
        metadata["CustomFeature"] = true;
        return metadata;
    }
}
```

---

### `DataTransformationPluginBase`
For plugins that transform data (encryption, compression, AI processing, watermarking).

**Inherits from:** `PluginBase`
**Implements:** `IDataTransformation`
**Category:** Automatically set to `DataTransformationProvider`

**What you must implement:**
```csharp
public abstract string SubCategory { get; }  // e.g., "Encryption", "Compression"
public abstract Stream OnWrite(Stream input, IKernelContext context, Dictionary<string, object> args);
public abstract Stream OnRead(Stream stored, IKernelContext context, Dictionary<string, object> args);
```

**What's provided for you:**
- Default `QualityLevel` (returns 50)
- Metadata generation with transformation details
- Streaming support indicators

**Example:**
```csharp
public class MyCompressionPlugin : DataTransformationPluginBase
{
    public override Guid Id => Guid.Parse("...");
    public override string Name => "Fast Compression";
    public override string SubCategory => "Compression";
    public override int QualityLevel => 30; // Fast compression

    public override Stream OnWrite(Stream input, IKernelContext context, Dictionary<string, object> args)
    {
        // Compress the stream
        return CompressStream(input);
    }

    public override Stream OnRead(Stream stored, IKernelContext context, Dictionary<string, object> args)
    {
        // Decompress the stream
        return DecompressStream(stored);
    }
}
```

---

### `StorageProviderPluginBase`
For plugins that provide storage backends (Local, S3, IPFS, Azure).

**Inherits from:** `PluginBase`
**Implements:** `IStorageProvider`
**Category:** Automatically set to `StorageProvider`

**What you must implement:**
```csharp
public abstract string Scheme { get; }  // e.g., "file", "s3", "ipfs"
public abstract Task SaveAsync(Uri uri, Stream data);
public abstract Task<Stream> LoadAsync(Uri uri);
public abstract Task DeleteAsync(Uri uri);
```

**What's provided for you:**
- Default `ExistsAsync()` implementation (tries to load)
- Metadata generation with storage type

**What you should override:**
- `ExistsAsync()` - For more efficient existence checks

**Example:**
```csharp
public class LocalFileSystemPlugin : StorageProviderPluginBase
{
    public override Guid Id => Guid.Parse("...");
    public override string Name => "Local File System";
    public override string Scheme => "file";

    public override async Task SaveAsync(Uri uri, Stream data)
    {
        var path = uri.LocalPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var file = File.Create(path);
        await data.CopyToAsync(file);
    }

    public override async Task<Stream> LoadAsync(Uri uri)
    {
        return File.OpenRead(uri.LocalPath);
    }

    public override Task DeleteAsync(Uri uri)
    {
        File.Delete(uri.LocalPath);
        return Task.CompletedTask;
    }

    public override Task<bool> ExistsAsync(Uri uri)
    {
        return Task.FromResult(File.Exists(uri.LocalPath));
    }
}
```

---

### `MetadataIndexPluginBase`
For plugins that index and search metadata (SQLite, Postgres, Vector DB).

**Inherits from:** `PluginBase`
**Implements:** `IMetadataIndex`
**Category:** Automatically set to `MetadataIndexingProvider`

**What you must implement:**
```csharp
public abstract Task IndexManifestAsync(Manifest manifest);
public abstract Task<string[]> SearchAsync(string query, float[]? vector, int limit);
public abstract IAsyncEnumerable<Manifest> EnumerateAllAsync(CancellationToken ct = default);
public abstract Task UpdateLastAccessAsync(string id, long timestamp);
public abstract Task<Manifest?> GetManifestAsync(string id);
public abstract Task<string[]> ExecuteQueryAsync(string query, int limit);
```

**What's provided for you:**
- Default `ExecuteQueryAsync(CompositeQuery query, int limit)` - Converts to text query
- Metadata indicating search capabilities (full-text, semantic, vector)

**AI-Native Features:**
- Built-in support for vector embeddings
- Semantic search capabilities
- Composite query support

**Example:**
```csharp
public class SqliteIndexPlugin : MetadataIndexPluginBase
{
    public override Guid Id => Guid.Parse("...");
    public override string Name => "SQLite Index";

    public override async Task IndexManifestAsync(Manifest manifest)
    {
        // Insert into SQLite database
    }

    public override async Task<string[]> SearchAsync(string query, float[]? vector, int limit)
    {
        // Search database, optionally using vector similarity
    }

    // ... implement other methods
}
```

---

### `FeaturePluginBase`
For plugins with lifecycle management (SQL Listener, Consensus, Governance).

**Inherits from:** `PluginBase`
**Implements:** `IFeaturePlugin`

**What you must implement:**
```csharp
public abstract Task StartAsync(CancellationToken ct);
public abstract Task StopAsync();
```

**What's provided for you:**
- Metadata indicating lifecycle management requirements

**Example:**
```csharp
public class SqlListenerPlugin : FeaturePluginBase
{
    public override Guid Id => Guid.Parse("...");
    public override string Name => "SQL Listener";
    public override PluginCategory Category => PluginCategory.OrchestrationProvider;

    public override async Task StartAsync(CancellationToken ct)
    {
        // Start listening on SQL port
    }

    public override async Task StopAsync()
    {
        // Stop the listener gracefully
    }
}
```

---

### `SecurityProviderPluginBase`
For security-related plugins (ACL, Authentication, Authorization).

**Inherits from:** `PluginBase`
**Category:** Automatically set to `SecurityProvider`

**What you must implement:**
- Core security logic specific to your plugin

**What's provided for you:**
- Metadata indicating security capabilities

---

### `OrchestrationProviderPluginBase`
For workflow and pipeline plugins (Workflow Engine, Pipeline Builder).

**Inherits from:** `PluginBase`
**Category:** Automatically set to `OrchestrationProvider`

**What you must implement:**
- Core orchestration logic

**What's provided for you:**
- Metadata indicating orchestration capabilities
- AI orchestration support flag

---

## Key Interfaces

### `IPlugin`
Base contract for all plugins.

**Properties:**
- `Guid Id` - Unique plugin identifier
- `PluginCategory Category` - Plugin category enum
- `string Name` - Human-readable name
- `string Version` - Semantic version

**Methods:**
- `Task<HandshakeResponse> OnHandshakeAsync(HandshakeRequest request)` - Plugin initialization
- `Task OnMessageAsync(PluginMessage message)` - Handle external messages

### `IDataTransformation`
Contract for data transformation plugins.

**Additional Properties:**
- `string SubCategory` - Specific transformation type
- `int QualityLevel` - Quality score (1-100)

**Methods:**
- `Stream OnWrite(Stream input, IKernelContext context, Dictionary<string, object> args)` - Transform during write
- `Stream OnRead(Stream stored, IKernelContext context, Dictionary<string, object> args)` - Transform during read

### `IStorageProvider`
Contract for storage provider plugins.

**Properties:**
- `string Scheme` - URI scheme (e.g., "s3", "file")

**Methods:**
- `Task SaveAsync(Uri uri, Stream data)` - Save data
- `Task<Stream> LoadAsync(Uri uri)` - Load data
- `Task DeleteAsync(Uri uri)` - Delete data
- `Task<bool> ExistsAsync(Uri uri)` - Check existence

### `IMetadataIndex`
Contract for metadata indexing plugins.

**Methods:**
- `Task IndexManifestAsync(Manifest manifest)` - Index a manifest
- `Task<string[]> SearchAsync(string query, float[]? vector, int limit)` - Search with optional vector
- `IAsyncEnumerable<Manifest> EnumerateAllAsync(CancellationToken ct)` - Enumerate all manifests
- `Task UpdateLastAccessAsync(string id, long timestamp)` - Update access time
- `Task<Manifest?> GetManifestAsync(string id)` - Get by ID
- `Task<string[]> ExecuteQueryAsync(string query, int limit)` - Execute text query
- `Task<string[]> ExecuteQueryAsync(CompositeQuery query, int limit)` - Execute composite query

### `IFeaturePlugin`
Contract for feature plugins with lifecycle.

**Methods:**
- `Task StartAsync(CancellationToken ct)` - Start the feature
- `Task StopAsync()` - Stop the feature

---

## AI-Native Features

### Execution Context (`IExecutionContext`)
Provides rich context for AI-driven operations:

```csharp
public interface IExecutionContext
{
    string UserId { get; }
    ISecurityContext SecurityContext { get; }

    void LogInfo(string message);
    void LogWarning(string message);
    void LogError(string message, Exception? ex = null);

    // AI-native features
    Task<bool> RequestApprovalAsync(string message, string? reason = null);
    Task<object?> InvokeCapabilityAsync(string capabilityId, Dictionary<string, object> parameters);

    T? GetConfig<T>(string key, T? defaultValue = default);
    CancellationToken CancellationToken { get; }
}
```

### Plugin Messages (`PluginMessage`)
Supports both structured and unstructured communication:

```csharp
public class PluginMessage
{
    public string Type { get; init; }  // e.g., "ai.query", "config.changed"
    public object? Payload { get; init; }
    public DateTime Timestamp { get; init; }
    public string Source { get; init; }  // e.g., "AI Agent", "Kernel"
    public string? CorrelationId { get; init; }

    // AI-native: Natural language description for LLMs
    public string? Description { get; init; }

    public Dictionary<string, object> Metadata { get; init; }
}
```

### Plugin Capabilities
Expose capabilities for AI agent discovery:

```csharp
protected override List<PluginCapabilityDescriptor> GetCapabilities()
{
    return new List<PluginCapabilityDescriptor>
    {
        new()
        {
            CapabilityId = "storage.local.save",
            DisplayName = "Save to Local Storage",
            Description = "Saves data to the local file system with optional encryption",
            Category = CapabilityCategory.Storage,
            RequiredPermission = Permission.Write,
            RequiresApproval = false,
            ParameterSchemaJson = "{ \"type\": \"object\", \"properties\": { \"path\": { \"type\": \"string\" } } }"
        }
    };
}
```

---

## Best Practices

### 1. Always Inherit from Abstract Base Classes
❌ **Don't do this:**
```csharp
public class MyPlugin : IPlugin
{
    // Have to implement everything manually
}
```

✅ **Do this:**
```csharp
public class MyPlugin : PluginBase
{
    // Only implement what's unique to your plugin
}
```

### 2. Provide AI-Friendly Metadata
```csharp
protected override Dictionary<string, object> GetMetadata()
{
    var metadata = base.GetMetadata();
    metadata["Description"] = "Natural language description for AI agents";
    metadata["UseCases"] = new[] { "Use case 1", "Use case 2" };
    metadata["Limitations"] = "What this plugin cannot do";
    return metadata;
}
```

### 3. Declare Capabilities for AI Discovery
```csharp
protected override List<PluginCapabilityDescriptor> GetCapabilities()
{
    return new()
    {
        new()
        {
            CapabilityId = "unique.capability.id",
            DisplayName = "Human-readable name",
            Description = "What this capability does",
            Category = CapabilityCategory.Intelligence,
            ParameterSchemaJson = "{ JSON Schema }"
        }
    };
}
```

### 4. Use Dependency Injection
```csharp
protected override List<PluginDependency> GetDependencies()
{
    return new()
    {
        new()
        {
            RequiredInterface = "IMetadataIndex",
            IsOptional = false,
            Reason = "Needed for manifest lookups"
        }
    };
}
```

---

## Future-Proof Design

The SDK is designed to evolve without breaking changes:

1. **Abstract base classes** can add new methods with default implementations
2. **Metadata dictionaries** allow extensibility without schema changes
3. **Capability descriptors** enable runtime discovery of new features
4. **AI-native design** supports future AI models and agents
5. **Versioning** built into plugin handshake protocol

---

## Plugin Categories

- **DataTransformationProvider**: Encrypt, compress, transform data
- **StorageProvider**: File systems, cloud storage, distributed storage
- **MetadataIndexingProvider**: SQLite, Postgres, Vector databases
- **SecurityProvider**: ACL, authentication, authorization
- **OrchestrationProvider**: Workflows, pipelines, AI orchestration

---

## Summary

**For Plugin Developers:**
1. Choose the appropriate base class for your plugin type
2. Implement only the abstract methods (core business logic)
3. Override optional methods to customize behavior
4. Provide AI-friendly metadata and capabilities
5. Test with AI agents and LLMs

**For Kernel Developers:**
1. All plugins follow consistent patterns
2. Discovery and introspection is built-in
3. AI agents can understand and invoke plugins
4. Future features can be added without breaking plugins

**The SDK is:**
- ✅ AI-native from the ground up
- ✅ Future-proof and extensible
- ✅ Minimal implementation required
- ✅ Self-describing and discoverable
- ✅ Supports autonomous AI operations
