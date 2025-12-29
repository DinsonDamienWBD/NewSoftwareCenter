# Software Center - Global Architecture Metadata
**Version:** 2.0 (Sealed Kernel / Message-Driven)
**Architecture Style:** Strict Message-Driven Architecture (MDA) / Plugin-Based
**Framework:** .NET 9 (Core)

---

## 1. Executive Summary & Core Philosophy

The Software Center is a decoupled, modular platform designed around the **"Sealed Kernel"** principle.
1.  **The Kernel is Immutable:** The core logic (BackendManager/FrontendManager) handles *routing*, *security*, and *persistence*. It does not know *business logic*.
2.  **Modules are Capabilities:** All functional logic (Files, Settings, Updates) exists in Modules.
3.  **Communication is Indirect:** Components never reference each other. They communicate solely by exchanging **Messages** (Commands, Queries, Events) via the Kernel.

### 1.1 The Dependency Graph
The architecture enforces a strict one-way dependency flow to ensure stability.
```
mermaid
graph TD
    Host[Host Application (EXE)] --> BackendManager
    Host --> FrontendManager
    
    BackendManager[BackendManager (The Brain)] --> Core
    FrontendManager[FrontendManager (The Compositor)] --> Core
    
    BackendManager --> DataWarehouse[DataWarehouse (The Vault)]
    
    Modules[External Modules (DLLs)] --> Core
    
    subgraph "The Contract"
    Core[SoftwareCenter.Core]
    end
```

---

## 2. The Core Contract (SoftwareCenter.Core)
This is the only library that Modules reference. It contains the "vocabulary" of the system.

### 2.1 The Message Protocol
We do not use Interfaces for features (IFileSystem). We use Messages.
 - ICommand<TResult> / ICommand: A request to change state (Write). Maps to exactly one handler.
    . Example: InstallSoftwareCommand, UpdateSettingCommand.
 - IQuery<TResult>: A request to read state (Read-Only). Maps to exactly one handler.
    . Example: GetSystemInfoQuery, ListFilesQuery.
 - IEvent: A notification that something happened (Fire-and-Forget). Maps to zero or many subscribers.
    . Example: SoftwareInstalledEvent, ErrorOccurredEvent.

### 2.2 The Registry Objects
Modules use these objects to tell the Kernel what they can do.
```
C#

// The Definition of a Capability
public class RegistryEntry
{
    public Guid Id { get; set; }
    public string OwnerModuleId { get; set; }
    public Type MessageType { get; set; }    // What message do I handle?
    public Type HandlerType { get; set; }    // What class handles it?
    public MessagePriority Priority { get; set; } // Critical, High, Normal, Low
    
    // Auto-generated from XML Documentation & Attributes
    public string Description { get; set; }  
    public string UsageExample { get; set; } 
    public bool IsObsolete { get; set; }
}
```

---

## 3. The BackendManager ("The Brain")
The BackendManager is the central nervous system. It does not implement features; it routes traffic.

### 3.1 Dynamic Discovery & Reflection (The "Introspection" Engine)
On startup, the BackendManager performs the following sequence:
1. Assembly Scan: Looks into ./Modules/ for *.dll files implementing IModule.
2. Manifest Generation:
 - It extracts the IHandle<T> interfaces from the DLL.
 - It locates the accompanying .xml documentation file.
 - It parses the <summary> and <example> tags.
3. Registry Build: It constructs a SystemManifest object in memory.
4. Endpoint Exposure: It exposes a GetSystemManifestQuery so the UI can render the "Developer Docs" page.

### 3.2 Intelligent Routing (The Priority System)
Multiple modules can attempt to handle the same command (e.g., OpenFileCommand). The BackendManager routes based on Priority.
1.  **Host (SystemModule): Registers OpenFileCommand with Priority.Low (Uses Windows Default).
2.  **PowerUserModule: Registers OpenFileCommand with Priority.High (Opens in internal hex editor).
3.  **Result: When the UI sends OpenFileCommand, the BackendManager selects the High priority handler automatically.
4. Conflict Resolution:
 - Priority.Critical (System integrity, e.g., Emergency Stop)
 - Priority.High (User overrides, e.g., PowerUser Module)
 - Priority.Normal (Standard Module behavior)
 - Priority.Low (Default/Fallback behavior)
5. Runtime Check: If two modules register High for the same command, the Kernel throws a AmbiguousHandlerException on startup to prevent undefined behavior.

### 3.3 The Pipeline (Middleware)
Every message passes through a rigid pipeline before reaching a handler:
1. TraceMiddleware: Assigns a TraceId (Correlation ID) for full observability.
2. AuthMiddleware: Checks if the UserPrincipal has permission for this Command Type.
3. ValidationMiddleware: Runs FluentValidation rules attached to the message.
4. AuditMiddleware: Serializes the Command and stores it in the DataWarehouse audit log.
5. ExecutionMiddleware: Invokes the IHandle<T>.Handle() method via Reflection.

---

## 4. The DataWarehouse ("The Vault")
A unified storage mechanism. Modules do not own databases; they own "Rooms" within the Warehouse.

### 4.1 Storage Strategy
 - Physical Storage: Data/Warehouse/{ModuleId}/{CollectionName}.json
 - Memory Strategy: ConcurrentDictionary cache for high-frequency reads.

### 4.1.1 The "Room" API
```
C#
public interface IRoom
{
    // Atomic Write
    Task SaveAsync<T>(string key, T data);
    
    // Cached Read
    Task<T> GetAsync<T>(string key);
    
    // Collection Query (Simple LINQ support)
    IQueryable<T> Query<T>(string collectionName);
}
```

### 4.1.2 Concept: "Rooms"
 - Isolation: Data is segregated by Module ID (The "Room").
 - Concurrency: The Warehouse uses a ConcurrencyGate (Reader/Writer Lock) keyed to specific Record IDs.
 - Format: Data is stored as structured JSON on disk, indexed in memory for speed.
```
C#

// Usage inside a Module
public async Task Handle(SaveConfigCommand cmd)
{
    // 1. Get my room
    var room = _dataWarehouse.GetRoom("MyModuleId");
    
    // 2. Atomic Write (Thread-safe)
    await room.SaveAsync("config-key", cmd.ConfigData);
}
```

---

## 5. Developer Experience (The Help System)
To support the "Dynamic Discovery" requirement, the system includes a self-documenting engine.
 - Attribute: [Documentation("Description", "Usage")]
 - Mechanism:
    * Developer Action: Adds XML comments to their Command.
    ```
    C#
    /// <summary>
    /// Reboots the target server safely.
    /// </summary>
    /// <example>
    /// { "ServerId": "srv-01", "Force": false }
    /// </example>
    public class RebootServerCommand : ICommand { ... }
    ```
    * System Action: The DocumentationProvider parses this at startup.
    * User Action: User navigates to /sys/docs in the app.
    * Result: A clean, Swagger-like UI listing every available Command, Query, and Event in the system that updates whenever a new module is dropped in.

---

## 6. Detailed Implementation Plan
### 6.1 Folder Structure
```
Plaintext
/Src
    /Kernel
        /Core
            /Attributes       ([Documentation], [Priority])
            /Interfaces       (IModule, ICommand, IQuery, IEvent, IFrontendManager, IBackendManager, IPipeline, IDataWarehouse)
            /Models           (RegistryEntry, Result<T>)
  
        /BackendManager**
            /Pipeline         (AuthMiddleware, ValidationMiddleware)
            /Services         (BackendManager, MessageBus, DocumentationProvider)

        /FrontendManager**
            /Pipeline         (AuthMiddleware, ValidationMiddleware, FrontendHub)
            /Services         (FrontendManager, MessageBus, TemplateEngine)

        /DataWarehouse
            /Data             (DataWarehouse, ConcurrencyGate)
            /Rooms            (IRoom, RoomImplementation)
  
    /Host
        /Program.cs
        /Startup.cs
  
    /Modules
        /Modules.System   (File I/O, Process Mgmt)
        /Modules.Network  (Ping, DNS)

** Modules are compiled separately and dropped into /Modules at runtime.
** The BackendManager, FrontendManager and DataWarehouse projects reference the Core project only.
** The Host project references only the BackendManager, FrontendManager and DataWarehouse projects.
** Modules reference only the Core project.
** BackendManager and FrontendManager are as symmetric as possible to reduce cognitive load, and promote ease of use and maintenance.
```

### 6.2 Key Class Names (Suggestion)
 - Core:
    . IModule, IMessage, ICommand, IQuery, IEvent
    . RegistryEntry, UiRegistrationEntry
    . IDataWarehouse, IRoom
    . Result<T>

 - Kernel (BckendManager, FrontendManager):
    . MessageBus (The router)
    . HandlerRegistry (The map)
    . DocumentationProvider (The reflection engine)

 - Host:
    . ModuleLoader (Assembly loading context)
    . CompositionRoot (DI Container setup)