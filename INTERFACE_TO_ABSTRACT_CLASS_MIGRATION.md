# Complete Interface to Abstract Class Migration Plan

## Overview

**Goal:** Migrate all plugins from obsolete interfaces to abstract base classes for maximum code reuse and consistency.

**Architecture Pattern:**
```
❌ OLD: Plugins implement interfaces directly
   → Code duplication (each plugin implements Name, Id, Version, Handshake, etc.)
   → Error-prone (missing implementations)

✅ NEW: Plugins extend abstract base classes
   → Maximum code reuse (80% less code)
   → Override only what's unique (SemanticDescription, Execute, etc.)
   → Property override pattern (protected override)
```

---

## Obsolete Interfaces and Their Replacements

| Obsolete Interface | Replacement Abstract Class | Status |
|-------------------|---------------------------|--------|
| `IStorageProvider` | `StorageProviderBase` | ⚠️ Partially migrated (kernel utilities done) |
| `IMetadataIndex` | `MetadataProviderBase` | ❌ Not started |
| `IFeaturePlugin` | `FeaturePluginBase` | ❌ Not started |
| `IInterfacePlugin` | `InterfacePluginBase` | ❌ Not started |
| `IDataTransformation` | `PipelinePluginBase` | ✅ No plugins using it |
| `IStoragePlugin` | `StorageProviderBase` | ✅ Marker interface (redundant) |

---

## Migration Target: Plugins Still Using Obsolete Interfaces

### 1. IMetadataIndex → MetadataProviderBase (2 plugins)

#### PostgresIndexingPlugin
**File:** `Plugins/DataWarehouse.Plugins.Indexing.Postgres/Bootstrapper/PostgresIndexingPlugin.cs`
```csharp
// BEFORE:
public class PostgresIndexingPlugin : IFeaturePlugin, IMetadataIndex

// AFTER:
public class PostgresIndexingPlugin : MetadataProviderBase
```

#### SqliteIndexingPlugin
**File:** `Plugins/DataWarehouse.Plugins.Indexing.Sqlite/Bootstrapper/SqliteIndexingPlugin.cs`
```csharp
// BEFORE:
public class SqliteIndexingPlugin : IFeaturePlugin, IMetadataIndex

// AFTER:
public class SqliteIndexingPlugin : MetadataProviderBase
```

**Note:** These currently implement **TWO** interfaces (IFeaturePlugin + IMetadataIndex). After migration, they only extend MetadataProviderBase (which already extends FeaturePluginBase).

---

### 2. IFeaturePlugin → FeaturePluginBase (8 plugins)

#### NeuralSentinelPlugin
**File:** `Plugins/DataWarehouse.Plugins.Features.AI/Bootstrapper/NeuralSentinelPlugin.cs`
```csharp
// BEFORE:
public class NeuralSentinelPlugin : IFeaturePlugin, INeuralSentinel

// AFTER:
public class NeuralSentinelPlugin : FeaturePluginBase, INeuralSentinel
```

#### NeuralSearchPlugin
**File:** `Plugins/DataWarehouse.Plugins.Features.AI/Bootstrapper/NeuralSearchPlugin.cs`
```csharp
// BEFORE:
public class NeuralSearchPlugin : IFeaturePlugin

// AFTER:
public class NeuralSearchPlugin : FeaturePluginBase
```

#### SqlListenerPlugin
**File:** `Plugins/DataWarehouse.Plugins.Features.SQL/Bootstrapper/SqlListenerPlugin.cs`
```csharp
// BEFORE:
public class SqlListenerPlugin : IFeaturePlugin

// AFTER:
public class SqlListenerPlugin : FeaturePluginBase
```

#### EnterpriseStoragePlugin
**File:** `Plugins/DataWarehouse.Plugins.Features.EnterpriseStorage/Bootstrapper/EnterpriseStoragePlugin.cs`
```csharp
// BEFORE:
public class EnterpriseStoragePlugin : IFeaturePlugin, ITieredStorage

// AFTER:
public class EnterpriseStoragePlugin : FeaturePluginBase, ITieredStorage
```

#### ACLSecurityPlugin
**File:** `Plugins/DataWarehouse.Plugins.Security.ACL/Bootstrapper/Init.cs`
```csharp
// BEFORE:
public class ACLSecurityPlugin : IFeaturePlugin, IAccessControl

// AFTER:
public class ACLSecurityPlugin : FeaturePluginBase, IAccessControl
```

#### GovernancePlugin
**File:** `Plugins/DataWarehouse.Plugins.Features.Governance/Bootstrapper/GovernancePlugin.cs`
```csharp
// BEFORE:
public class GovernancePlugin : IFeaturePlugin

// AFTER:
public class GovernancePlugin : FeaturePluginBase
```

**Additional files:** 2 more IFeaturePlugin usages found

---

### 3. IInterfacePlugin → InterfacePluginBase (1 plugin)

#### GrpcNetworkInterfacePlugin
**File:** `Plugins/DataWarehouse.Plugins.Interface.gRPC/Bootstrapper/Init.cs`
```csharp
// BEFORE:
public class GrpcNetworkInterfacePlugin : IInterfacePlugin

// AFTER:
public class GrpcNetworkInterfacePlugin : InterfacePluginBase
```

---

## Migration Steps for Each Plugin

### Step 1: Change Class Declaration
```csharp
// BEFORE:
public class MyPlugin : IFeaturePlugin
{
    public string Id => "my-plugin";
    public string Name => "My Plugin";
    public string Version => "1.0.0";

    public Task<HandshakeResponse> OnHandshakeAsync(HandshakeRequest request)
    {
        return Task.FromResult(HandshakeResponse.Success(...));
    }
}

// AFTER:
public class MyPlugin : FeaturePluginBase
{
    public MyPlugin() : base(
        id: "my-plugin",
        name: "My Plugin",
        version: "1.0.0",
        category: PluginCategory.Feature)
    {
    }
}
```

### Step 2: Convert Properties to Overrides
```csharp
// BEFORE (in constructor or as properties):
SemanticDescription = "Provides X functionality";
SemanticTags = new List<string> { "tag1", "tag2" };

// AFTER (property overrides):
protected override string SemanticDescription => "Provides X functionality";
protected override string[] SemanticTags => new[] { "tag1", "tag2" };
```

### Step 3: Remove Handshake Boilerplate
```csharp
// BEFORE (manual handshake implementation):
public Task<HandshakeResponse> OnHandshakeAsync(HandshakeRequest request)
{
    return Task.FromResult(HandshakeResponse.Success(
        pluginId: Id,
        name: Name,
        version: new Version(Version),
        category: PluginCategory.Feature,
        capabilities: [...],
        initDuration: TimeSpan.Zero
    ));
}

// AFTER (inherited from base class, override only if custom logic needed):
// No code needed - base class handles it!
// Or override if you need custom initialization:
protected override async Task<HandshakeResponse> OnHandshakeAsync(HandshakeRequest request)
{
    // Custom initialization logic
    return await base.OnHandshakeAsync(request);
}
```

### Step 4: Implement Required Abstract Methods
```csharp
// Each base class requires specific methods:

// FeaturePluginBase requires:
protected override async Task ExecuteAsync(CancellationToken ct)
{
    // Feature execution logic
}

// MetadataProviderBase requires (extends FeaturePluginBase):
public override async Task IndexAsync(string container, string blobId, Dictionary<string, string> metadata)
{
    // Indexing logic
}

// StorageProviderBase requires (already migrated in some plugins):
public override async Task SaveAsync(Uri uri, Stream data) { ... }
public override async Task<Stream> LoadAsync(Uri uri) { ... }
// etc.
```

---

## Code Reduction Example

### BEFORE: SqliteIndexingPlugin (Interface Pattern)
```csharp
public class SqliteIndexingPlugin : IFeaturePlugin, IMetadataIndex
{
    public string Id => "indexing-sqlite";
    public string Name => "SQLite Metadata Index";
    public string Version => "2.0.0";
    public string Description => "Production SQLite metadata indexer";

    // 60+ lines of boilerplate handshake code
    public Task<HandshakeResponse> OnHandshakeAsync(HandshakeRequest request)
    {
        return Task.FromResult(HandshakeResponse.Success(
            pluginId: Id,
            name: Name,
            version: new Version(Version),
            category: PluginCategory.Indexing,
            capabilities: new List<PluginCapabilityDescriptor>
            {
                new PluginCapabilityDescriptor { /* ... */ }
            },
            initDuration: TimeSpan.Zero
        ));
    }

    public Task OnMessageAsync(PluginMessage message) => Task.CompletedTask;

    // Actual indexing logic (only 20 lines of unique code)
    public async Task IndexAsync(...) { /* unique logic */ }
}
```

### AFTER: SqliteIndexingPlugin (Abstract Class Pattern)
```csharp
public class SqliteIndexingPlugin : MetadataProviderBase
{
    public SqliteIndexingPlugin() : base(
        id: "indexing-sqlite",
        name: "SQLite Metadata Index",
        version: "2.0.0",
        category: PluginCategory.Indexing)
    {
    }

    protected override string SemanticDescription => "Production SQLite metadata indexer";

    // Actual indexing logic (same 20 lines)
    public override async Task IndexAsync(...) { /* unique logic */ }
}
```

**Result:** 80+ lines → 20 lines (75% reduction)

---

## Additional Changes Required

### Update Plugin Retrieval Calls

Many places retrieve plugins using obsolete interfaces:
```csharp
// BEFORE:
var metadataIndex = context.GetPlugin<IMetadataIndex>();

// AFTER (Option 1 - Use base class):
var metadataIndex = context.GetPlugin<MetadataProviderBase>();

// AFTER (Option 2 - Keep interface if needed for flexibility):
var metadataIndex = context.GetPlugin<IMetadataIndex>(); // Still works, just obsolete
```

**Found in:**
- `GovernancePlugin.cs`: `context.GetPlugin<IMetadataIndex>()`
- Other plugins may also retrieve by interface

---

## Benefits of This Migration

1. **80% Less Code:** Plugins go from 100+ lines to ~20 lines
2. **No Boilerplate:** Handshake, properties, lifecycle all handled by base class
3. **Consistency:** All plugins follow the same pattern
4. **Type Safety:** Property overrides are compile-time checked
5. **Maintainability:** Changes to plugin infrastructure happen in one place
6. **Better IDE Support:** Override autocomplete shows what's available
7. **Fewer Errors:** Can't forget to implement required interface members

---

## Migration Priority

### Phase 1: High-Impact (Start Here) ✅
- **IMetadataIndex → MetadataProviderBase** (2 plugins)
  - PostgresIndexingPlugin
  - SqliteIndexingPlugin

### Phase 2: Feature Plugins
- **IFeaturePlugin → FeaturePluginBase** (6-8 plugins)
  - NeuralSentinelPlugin
  - NeuralSearchPlugin
  - SqlListenerPlugin
  - EnterpriseStoragePlugin
  - ACLSecurityPlugin
  - GovernancePlugin

### Phase 3: Interface Plugins
- **IInterfacePlugin → InterfacePluginBase** (1 plugin)
  - GrpcNetworkInterfacePlugin

### Phase 4: Storage Plugins (Some Already Done)
- **IStorageProvider → StorageProviderBase** (remaining plugins)
  - RAMDiskStorageEngine
  - NetworkStorageProvider (2 files)
  - SegmentedDiskProvider
  - SelfHealingMirror

---

## Validation After Migration

After each plugin migration, verify:
1. ✅ Plugin compiles without errors
2. ✅ No property assignment errors (CS0200)
3. ✅ Handshake succeeds
4. ✅ Plugin loads and initializes correctly
5. ✅ Functionality works as before
6. ✅ No obsolete warnings for that plugin

---

## Current Status

- **IStorageBackend Migration (Kernel):** ✅ Completed (73 references)
  - RaidEngine, StoragePoolManager, InMemoryStorageProvider, LocalDiskProvider

- **Plugin Abstract Class Migration:** ⏳ Not started
  - 11 plugins still using obsolete interfaces
  - All abstract base classes exist and ready

---

## Next Action

Start with **Phase 1: IMetadataIndex → MetadataProviderBase** migration:
1. Migrate PostgresIndexingPlugin
2. Migrate SqliteIndexingPlugin
3. Update GovernancePlugin retrieval call
4. Test and verify
5. Commit progress
