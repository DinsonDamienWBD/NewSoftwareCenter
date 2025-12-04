# SoftwareCenter.Core - Project Structure

## Root: SoftwareCenterRefactored/Core/

### /Common
- TraceHop.cs
- TraceContext.cs

### /Commands
- ICommand.cs       # Input contract (Name, Params, Requestor)
- IResult.cs        # Output contract (Success, Data, Requestor, Servicer)
- Result.cs

### /Data
- IGlobalDataStore.cs
- DataEntry.cs
- DataPolicy.cs

### /Events
- IEvent.cs
- IEventBus.cs

### /Modules
- IModule.cs        # Lifecycle contract (Init, Unload)

### /Routing
- IRouter.cs        # The bridge between Host and Kernel
- CommandStatus.cs
- RouteDefinition.cs

### /UI
- UIZone.cs         # Enum for the 5 mandatory zones
- ContentPart.cs    # Generic container for UI elements - supports tracing
- UIControl.cs

### /Logging
- LogLevel.cs       # Enum

### /Project Metadata
- Project_Structure.md
- Project_Implementation_Plan.md
- Project_History_Log.md