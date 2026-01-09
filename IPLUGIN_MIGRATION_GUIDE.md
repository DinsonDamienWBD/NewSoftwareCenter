# IPlugin Interface Migration Guide

## Overview
The IPlugin interface has been refactored to use a pure message-based handshake protocol. This breaking change removes direct property access to `Id`, `Name`, and `Version`, moving all metadata to the `HandshakeResponse`.

## Breaking Changes

### 1. Removed Properties from IPlugin
```csharp
// BEFORE (OLD)
public interface IPlugin
{
    string Id { get; }
    string Name { get; }
    string Version { get; }
    void Initialize(IKernelContext context);
}

// AFTER (NEW)
public interface IPlugin
{
    Task<HandshakeResponse> OnHandshakeAsync(HandshakeRequest request);
    Task OnMessageAsync(PluginMessage message); // Optional, default no-op
}
```

### 2. Metadata Now in HandshakeResponse
All plugin metadata (Id, Name, Version, Category, Capabilities, Dependencies) is now returned in the `HandshakeResponse`:
```csharp
public class HandshakeResponse
{
    public string PluginId { get; init; }
    public string Name { get; init; }
    public Version Version { get; init; }
    public PluginCategory Category { get; init; }
    public bool Success { get; init; }
    public PluginReadyState ReadyState { get; init; }
    public List<PluginCapabilityDescriptor> Capabilities { get; init; }
    public List<PluginDependency> Dependencies { get; init; }
    // ... more fields
}
```

## Migration Steps

### For Plugins Using PluginBase

**GOOD NEWS:** If your plugin inherits from `PluginBase` or `StorageProviderBase`, NO CHANGES NEEDED! These base classes already implement the new protocol.

```csharp
// This plugin is already compatible - no changes needed!
public class MyStoragePlugin : StorageProviderBase
{
    public MyStoragePlugin() : base(
        id: "MyStorage",
        name: "My Storage Provider",
        version: new Version(1, 0, 0))
    {
    }

    protected override string StorageType => "mystorage";
    // ... implement abstract methods ...
}
```

### For Plugins Directly Implementing IPlugin

If your plugin directly implements `IPlugin` (not using base classes), follow these steps:

#### Step 1: Remove Old Properties
```csharp
// REMOVE THESE:
public string Id => "MyPlugin";
public string Name => "My Plugin";
public string Version => "1.0.0";
public void Initialize(IKernelContext context) { /* ... */ }
```

#### Step 2: Implement OnHandshakeAsync
```csharp
public async Task<HandshakeResponse> OnHandshakeAsync(HandshakeRequest request)
{
    try
    {
        // 1. Store context
        _context = CreateContextFromRequest(request);

        // 2. Initialize your plugin
        await InitializeInternalAsync();

        // 3. Return success response
        return HandshakeResponse.Success(
            pluginId: "MyPlugin",
            name: "My Plugin",
            version: new Version(1, 0, 0),
            category: PluginCategory.Storage); // or appropriate category
    }
    catch (Exception ex)
    {
        return HandshakeResponse.Failure(
            "MyPlugin",
            "My Plugin",
            $"Initialization failed: {ex.Message}");
    }
}

private IKernelContext CreateContextFromRequest(HandshakeRequest request)
{
    // Create a lightweight IKernelContext wrapper
    // See PluginBase.KernelContextWrapper for reference implementation
}
```

#### Step 3: Implement OnMessageAsync (Optional)
```csharp
public Task OnMessageAsync(PluginMessage message)
{
    // Handle runtime messages (health checks, shutdown, etc.)
    // For simple plugins, just return Task.CompletedTask
    return Task.CompletedTask;
}
```

### For Code Accessing Plugin Properties

#### Old Code (Broken):
```csharp
var plugin = registry.GetPlugin<IStorageProvider>();
Console.WriteLine($"Using plugin: {plugin.Name} v{plugin.Version}");
string pluginId = plugin.Id;
```

#### New Code (Fixed):
```csharp
var plugin = registry.GetPlugin<IStorageProvider>();
var response = registry.GetPluginResponse(plugin.GetType().FullName); // NEW METHOD
if (response != null)
{
    Console.WriteLine($"Using plugin: {response.Name} v{response.Version}");
    string pluginId = response.PluginId;
}
```

**BETTER:** Store the `HandshakeResponse` when you register the plugin:
```csharp
// During registration
var response = await plugin.OnHandshakeAsync(request);
registry.Register(plugin, response); // Pass both!

// Later retrieval
var pluginMeta = registry.GetPluginResponse("MyPluginId");
Console.WriteLine($"{pluginMeta.Name} v{pluginMeta.Version}");
```

### For PluginRegistry.Register() Calls

#### Old Code (Broken):
```csharp
var plugin = new MyPlugin();
plugin.Initialize(context);
registry.Register(plugin); // MISSING HandshakeResponse!
```

#### New Code (Fixed):
```csharp
var plugin = new MyPlugin();
var request = new HandshakeRequest
{
    KernelId = kernelId,
    ProtocolVersion = "1.0",
    Mode = OperatingMode.Laptop,
    RootPath = "/path/to/data",
    AlreadyLoadedPlugins = registry.GetAllDescriptors()
};

var response = await plugin.OnHandshakeAsync(request);
if (response.Success)
{
    registry.Register(plugin, response); // Pass both!
}
```

## Common Migration Patterns

### Pattern 1: Plugin with Initialization Logic
```csharp
// OLD
public void Initialize(IKernelContext context)
{
    _logger.LogInfo("Initializing...");
    ConnectToDatabase();
    LoadConfiguration();
}

// NEW
public async Task<HandshakeResponse> OnHandshakeAsync(HandshakeRequest request)
{
    try
    {
        _context = WrapRequest(request);
        _context.LogInfo("Initializing...");
        await ConnectToDatabaseAsync();
        await LoadConfigurationAsync();

        return HandshakeResponse.Success(
            pluginId: "MyPlugin",
            name: "My Plugin",
            version: new Version(1, 0, 0),
            category: PluginCategory.Storage);
    }
    catch (Exception ex)
    {
        return HandshakeResponse.Failure("MyPlugin", "My Plugin", ex.Message);
    }
}
```

### Pattern 2: Plugin Calling Another Plugin's Initialize
```csharp
// OLD
_networkProvider = new NetworkStorageProvider();
_networkProvider.Initialize(context);
Console.WriteLine($"Connected to: {_networkProvider.Id}");

// NEW
_networkProvider = new NetworkStorageProvider();
var request = new HandshakeRequest { /* ... */ };
var response = await _networkProvider.OnHandshakeAsync(request);
if (!response.Success)
{
    throw new Exception($"Network provider failed: {response.ErrorMessage}");
}
Console.WriteLine($"Connected to: {response.PluginId}");
```

### Pattern 3: Dynamic Plugin Compilation (TheArchitect pattern)
```csharp
// OLD
var plugin = (IPlugin)Activator.CreateInstance(type)!;
plugin.Initialize(context);
registry.Register(plugin);
return plugin.Id;

// NEW
var plugin = (IPlugin)Activator.CreateInstance(type)!;
var handshakeRequest = new HandshakeRequest
{
    KernelId = Guid.NewGuid().ToString(),
    ProtocolVersion = "1.0",
    Mode = context.Mode,
    RootPath = context.RootPath,
    AlreadyLoadedPlugins = registry.GetAllDescriptors()
};

var response = await plugin.OnHandshakeAsync(handshakeRequest);
if (!response.Success)
{
    throw new InvalidOperationException($"Plugin handshake failed: {response.ErrorMessage}");
}

registry.Register(plugin, response);
return response.PluginId;
```

## Files That Need Updates

### Critical Core Files (ALREADY FIXED):
- [x] `DataWarehouse.SDK/Contracts/IPlugin.cs` - Interface definition
- [x] `DataWarehouse.SDK/Contracts/PluginBase.cs` - Base class (removed deprecated properties)
- [x] `DataWarehouse.SDK/Services/PluginRegistry.cs` - Register() signature changed
- [x] `DataWarehouse.Kernel/Engine/HandshakePluginLoader.cs` - Uses new Register() signature
- [x] `DataWarehouse.Plugins.Features.AI/Engine/TheArchitect.cs` - Dynamic compilation
- [x] `DataWarehouse.Plugins.Features.EnterpriseStorage/Engine/SelfHealingMirror.cs` - Direct IPlugin impl

### Plugin Bootstrap Files (NEED FIXES):
- [ ] `DataWarehouse.Plugins.Interface.gRPC/Bootstrapper/Init.cs` (partially fixed)
- [ ] `DataWarehouse.Plugins.Storage.RAMDisk/Bootstrapper/Init.cs`
- [ ] `DataWarehouse.Plugins.Features.EnterpriseStorage/Bootstrapper/EnterpriseStoragePlugin.cs`
- [ ] `DataWarehouse.Plugins.Storage.IpfsNew/Bootstrapper/*.cs`
- [ ] `DataWarehouse.Plugins.Storage.S3New/Bootstrapper/*.cs`
- [ ] `DataWarehouse.Plugins.Indexing.Postgres/Bootstrapper/*.cs`
- [ ] `DataWarehouse.Plugins.Indexing.Sqlite/Bootstrapper/*.cs`
- [ ] `DataWarehouse.Plugins.Features.SQL/Bootstrapper/*.cs`
- [ ] `DataWarehouse.Plugins.Features.Governance/Bootstrapper/*.cs`
- [ ] `DataWarehouse.Plugins.Features.AI/Bootstrapper/*.cs`
- [ ] `DataWarehouse.Plugins.Features.Consensus/Bootstrapper/*.cs`

### Storage Engine Files (MAY NEED FIXES):
- [ ] `DataWarehouse.Plugins.Storage.IpfsNew/Engine/IPFSStorageEngine.cs`
- [ ] `DataWarehouse.Plugins.Storage.S3New/Engine/S3StorageEngine.cs`
- [ ] `DataWarehouse.Plugins.Storage.RAMDisk/Engine/RAMDiskStorageEngine.cs`
- [ ] `DataWarehouse.Plugins.Interface.gRPC/Engine/NetworkStorageProvider.cs`
- [ ] `DataWarehouse.Plugins.Features.EnterpriseStorage/Engine/NetworkStorageProvider.cs`

## Testing Your Migration

After migrating, verify:

1. **Compilation:** Project builds without errors
2. **Plugin Loading:** Plugins load successfully via HandshakePluginLoader
3. **Metadata Access:** Can retrieve plugin metadata via registry.GetPluginResponse()
4. **Runtime:** Plugins function correctly after handshake

## Quick Reference: Method Signature Changes

| Component | Old Signature | New Signature |
|-----------|--------------|---------------|
| IPlugin.Initialize | `void Initialize(IKernelContext)` | `Task<HandshakeResponse> OnHandshakeAsync(HandshakeRequest)` |
| IPlugin.Id | `string Id { get; }` | ❌ REMOVED (use HandshakeResponse.PluginId) |
| IPlugin.Name | `string Name { get; }` | ❌ REMOVED (use HandshakeResponse.Name) |
| IPlugin.Version | `string Version { get; }` | ❌ REMOVED (use HandshakeResponse.Version) |
| PluginRegistry.Register | `Register(IPlugin)` | `Register(IPlugin, HandshakeResponse)` |
| PluginBase.OnMessageAsync | `Task<MessageResponse> OnMessageAsync(...)` | `Task OnMessageAsync(...)` (returns void Task) |

## Need Help?

- See `PluginBase.cs` for a complete reference implementation
- See `StorageProviderBase.cs` for storage-specific example
- See `HandshakePluginLoader.cs` for loading workflow
- See `PluginMessages.cs` for message protocol details

## Estimated Impact

- **Total Files Affected:** ~80+
- **Critical Files:** 6 (FIXED)
- **Plugin Bootstrappers:** ~15 (NEED FIX)
- **Storage Engines:** ~5 (NEED FIX)
- **Estimated Time:** 2-4 hours for systematic migration
