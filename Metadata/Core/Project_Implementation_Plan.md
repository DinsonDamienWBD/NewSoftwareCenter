# SoftwareCenter.Core - Implementation Plan

## STRATEGY: Build-Once
This project is the static foundation. It contains ZERO logic, only contracts.
Namespace: `SoftwareCenter.Core`

## PHASE 1: The Command Bus & Tracing (Critical)
**Goal:** Define the messaging envelope with automated traceability.
- [x] Create `Common/TraceHop.cs` (EntityId, Action, Timestamp)
- [x] Create `Common/TraceContext.cs` (AsyncLocal for causal linking)
- [x] Create `Commands/ICommand.cs`
    - Props: Name, Params
    - Trace: TraceId (Guid), History (List<TraceHop>)
- [x] Create `Commands/IResult.cs`
    - Props: Success, Message, Data
    - Trace: TraceId, History

## PHASE 2: The Module System
- [x] Create `Modules/IModule.cs` (RegisterAsync, UnregisterAsync)
- [x] Create `Kernel/IKernel.cs` (Inherits IRouter)
- [x] Create `Routing/IRouter.cs`

## PHASE 3: UI Abstractions (High)
**Goal:** Define abstract UI containers with Override capabilities.
- [x] Create `UI/UIZone.cs` (Enum: Title, Notif, Power, Nav, Content)
- [x] Create `UI/ContentPart.cs`
    - Props: ViewId, TargetZone, ContentObject
    - **Feature:** Added `RegionName` (string) and `Priority` (int) to support Module overrides of Host UI.

## PHASE 4: Shared Types (Medium)
**Goal:** Common Enums and DTOs.
- [x] Create `Logging/LogLevel.cs`
- [x] Create `Logging/LogEntry.cs` (DTO for passing logs to Host)

## PHASE 5: Event Bus (Reactive)
**Goal:** Enable Pub/Sub for modules (e.g., AI Agent).
- [x] Create `Events/IEvent.cs` (Name, Data, Timestamp, SourceId)
    - Trace: TraceId (Linked to Command), SourceId (Auto-stamped)
- [x] Create `Events/IEventBus.cs` (Publish, Subscribe, Unsubscribe)

## PHASE 6: Global Data Store (Shared Memory)
**Goal:** Define contract for RAM/Disk storage with metadata.
- [x] Create `Data/DataPolicy.cs` (Transient/Persistent)
- [x] Create `Data/DataEntry.cs` (Wrapper with Timestamp, SourceId, TraceId)
- [x] Create `Data/IGlobalDataStore.cs` (Async Store/Retrieve)