# Kernel Implementation Plan

## Goal

Goal: Implement the system's BFF (Backend for Frontend) orchestrator "Brain" which takes absolute control over routing.
Scope: Implement the 7 Core Features defined in the architecture.

## Architecture: "Complete Takeover"
1.  **Host Starts**: Uses temporary `BasicRouter`.
2.  **Kernel Loads**:
    * Host injects its native capabilities (File IO, Cache) into Kernel.
    * Kernel registers them into `HandlerRegistry` (Priority: HostDefault).
    * Kernel **replaces** `BasicRouter`.
3.  **Runtime**: `SmartRouter` handles *everything*. If a command isn't in the Registry, it can fallback depending on the commands 'allowFallback' flag. If cannot fallback, or fallback fails, fail gracefully.

## Phase 1: The Registry & Data Store
- [X] **Handler Registry**:
    -   Implements a thread-safe `ConcurrentDictionary <<string, List<HandlerEntry>>`.
    -   Dynamic: Methods `RegisterRange(...)` and `RemoveRange(...)` are called by the Loader in real-time.
    -   Logic: Support explicit priorities (High/Normal/Low). ConcurrentDictionary with Priority Sorting (High > Low).
- [X] **Global Data Store**:
    -   Implement `IGlobalDataStore` using **LiteDB**.
    -   Ensure thread-safe access for Host and Modules.
    -   Requirement: Must support Store<T> and Retrieve<T> for complex objects.
- [X] **Event Bus**: 
    -   Implement DefaultEventBus.
    -   Requirement: Must propagate TraceContext with every event.
- [X] **Command Bus**: 


## Phase 1.5: The Dynamic Catalog (Refined)
- [ ] **Capability Provider**:
    -   Queries the `HandlerRegistry`.
    -   Returns `IEnumerable<RouteDefinition>` (from Core) to the Host/UI.
    -   This ensures the UI always shows exactly what is currently loaded. 
- [ ] **Discovery**: 
    -   GetRegistryManifest() for UI generation.

## Phase 2: The Smart Router (The Authority)
- [X] **Routing Logic**:
    -   `ExecuteAsync(cmd)`: Strictly lookup in `HandlerRegistry`.
- [ ] **Fallback**: 
    -   Depends on Command flags/availability. If command missing -> Return `Result.NotFound`. Else, if command fails, fallback based on flag. If no fallback, return 'Result.Failure'
- [ ] **Trace Injection**:
    -   Automatically create/attach TraceContext to every command.
- [X] **Metadata Gate**: 
    -   Enforce CommandStatus.Obsolete (Block) and CommandStatus.Deprecated (Warn).
- [X] **Exception Barrier**:
    -   Wrap generic delegate execution in `try/catch`.

## Phase 3: Intelligent Logging (Serilog Ready)
- [X] **Contract**:
    -   Execute("System.Log", Dictionary<string, object> payload)
- [X] **The "Overtake" Strategy**:
    -   Host (Priority 0): BasicLogger. Looks for payload["Message"]. Writes to text file.
    -   LogNotifManager (Priority 100): AdvancedLogger. Looks for payload["Level"], payload["Exception"]. Uses Serilog/Cloud.
    -   Kernel Middleware: The Router detects System.Log and injects payload["TraceContext"] before passing to handler. This ensures the logger (whoever it is) has full context.

## Phase 4: The Module Loader & Kernel Service(The Plugin System)
- [X] **Discovery**: Scan `Modules/`.
- [X] **Isolation**: `ModuleLoadContext` (AssemblyLoadContext).
- [ ] **Hot-Swap**: Unload context -> Remove handlers -> GC.
- [ ] **Isolation**: Use AssemblyLoadContext to prevent DLL hell between modules.
- [X] **Injection**: Pass IKernel to IModule.InitializeAsync.
- [X] **Concrete Service**: KernelService aggregates Router, Registry, and Store.

## Phase 5: Integration (The Handshake)
- [ ] **Kernel Initialization**:
    -   Expose `Register(...)`
    -   Let Host and Modules register their own capabilities during their own initialization sequence.
    -   Provide the Init contract interface with the necessary functions & signature to enforce them to register their capabilities as standard handlers.

## Phase 6: Technical Standards
- [ ] **Extensibility Protocol**:
    -   Loose Coupling: All advanced commands (AI, WinRM, Log) use Dictionary<string, object> as arguments.
    -   Why: The Kernel never needs to reference System.Management.Automation (PowerShell) or OpenAI SDKs. It blindly passes the dictionary to the module that owns it.
- [ ] **Global Data Store**:
    -   Use LiteDB for simplicity and zero-dependency.
    -   File: %AppData%/SoftwareCenter/GlobalDataStore.db
    -   Lifecycle: Opened on Kernel Start, Closed on Kernel Stop. Single-instance access.