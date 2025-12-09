# Project: SoftwareCenter.Core

## Purpose
`SoftwareCenter.Core` is the foundational contract library for the entire Software Center ecosystem. It defines the essential, non-negotiable interfaces, abstract classes, and data models that enable the decoupled, modular architecture. It is designed to be minimal, lightweight, and extremely stable. It provides the contracts for UI elements and interactions (commands, events), but the main interfaces and implementations for the application kernel (`IKernel`) and UI engine (`IUIEngine`) are intended to reside in other projects (e.g., Kernel, UIManager).

## Key Responsibilities
- Define the `IModule` interface, the primary contract for any loadable module.
- Define the patterns for communication: `ICommand`, `ICommandHandler`, `IEvent`, `IEventHandler`.
- Define contracts for runtime discovery, such as `IApiEndpoint`.
- Define the `ITraceContext` for passing diagnostic information through the system.
- Provide base abstractions from `Microsoft.Extensions.*` to ensure a common language for Dependency Injection and Logging without enforcing a specific implementation.

## Architectural Principles
- **Minimalism:** Contains only what is absolutely necessary for the framework to function. It does not contain contracts for specific services like logging or installation.
- **Stability:** This assembly should change very infrequently. Changes here are breaking changes for the entire ecosystem.
- **The Shared Language:** This is the only assembly that third-party module developers should need to reference to build a compatible module.

## Project Structure
```
SoftwareCenter.Core/
├── Commands/
│   ├── ICommand.cs
│   └── ICommandHandler.cs
├── Diagnostics/
│   └── ITraceContext.cs
├── Events/
│   ├── IEvent.cs
│   └── IEventHandler.cs
├── Modules/
│   └── IModule.cs
├── Routing/
│   └── IApiEndpoint.cs
├── SoftwareCenter.Core.csproj
└── METADATA.md
```

## Dependencies
- `Microsoft.Extensions.DependencyInjection.Abstractions`
- `Microsoft.Extensions.Logging.Abstractions`

## Detailed API/Component List

### `Contract, UI & Routing/Core/SoftwareCenter.Core.csproj`
- Project file for SoftwareCenter.Core.

### `Contract, UI & Routing/Core/METADATA.md`
- Project metadata file.

### `Contract, UI & Routing/Core/Attributes/HandlerPriorityAttribute.cs`
- **Class Name:** `HandlerPriorityAttribute`
- **Inherits from:** `Attribute`
- **Properties:**
    - `Priority` (int, read-only)
- **Constructor:**
    - `HandlerPriorityAttribute(int priority)`

### `Contract, UI & Routing/Core/Commands/ICommand.cs`
- **Interface Name:** `ICommand`
- **Interface Name:** `ICommand<TResult>`

### `Contract, UI & Routing/Core/Commands/ICommandHandler.cs`
- **Interface Name:** `ICommandHandler<in TCommand>` (where `TCommand : ICommand`)
    - **Function:** `Task Handle(TCommand command, ITraceContext traceContext)`
        - **Parameters:** `command` (`TCommand`), `traceContext` (`ITraceContext`)
        - **Returns:** `Task`
- **Interface Name:** `ICommandHandler<in TCommand, TResult>` (where `TCommand : ICommand<TResult>`)
    - **Function:** `Task<TResult> Handle(TCommand command, ITraceContext traceContext)`
        - **Parameters:** `command` (`TCommand`), `traceContext` (`ITraceContext`)
        - **Returns:** `Task<TResult>`

### `Contract, UI & Routing/Core/Commands/ICommandValidator.cs`
- **Interface Name:** `ICommandValidator<in TCommand>` (where `TCommand : ICommand`)
    - **Function:** `Task Validate(TCommand command, ITraceContext traceContext)`
        - **Parameters:** `command` (`TCommand`), `traceContext` (`ITraceContext`)
        - **Returns:** `Task`

### `Contract, UI & Routing/Core/Commands/LogCommand.cs`
- **Class Name:** `LogCommand`
- **Implements:** `ICommand`
- **Properties:**
    - `Level` (LogLevel, read-only)
    - `Message` (string, read-only)
    - `ExceptionDetails` (string, read-only)
    - `TraceId` (Guid, read-only)
    - `InitiatingModuleId` (string, read-only)
    - `StructuredData` (Dictionary<string, object>, read-only)
- **Constructor:**
    - `LogCommand(LogLevel level, string message, ITraceContext traceContext, Exception exception = null, Dictionary<string, object> structuredData = null)`

### `Contract, UI & Routing/Core/Commands/UI/CreateElementCommand.cs`
- **Class Name:** `CreateElementCommand`
- **Implements:** `ICommand<Guid>`
- **Properties:**
    - `ElementType` (ElementType, read-only)
    - `ParentId` (Guid?, read-only)
    - `InitialProperties` (Dictionary<string, object>, read-only)
- **Constructor:**
    - `CreateElementCommand(ElementType elementType, Guid? parentId = null, Dictionary<string, object> initialProperties = null)`

### `Contract, UI & Routing/Core/Commands/UI/CreateUIElementCommand.cs`
- **Class Name:** `CreateUIElementCommand`
- **Implements:** `ICommand<string>`
- **Properties:**
    - `ParentId` (string, read-only)
    - `ElementType` (string, read-only)
    - `InitialProperties` (Dictionary<string, object>, read-only)
- **Constructor:**
    - `CreateUIElementCommand(string parentId, string elementType, Dictionary<string, object> initialProperties = null)`

### `Contract, UI & Routing/Core/Commands/UI/DeleteUIElementCommand.cs`
- **Class Name:** `DeleteUIElementCommand`
- **Implements:** `ICommand`
- **Properties:**
    - `ElementId` (string, read-only)
- **Constructor:**
    - `DeleteUIElementCommand(string elementId)`

### `Contract, UI & Routing/Core/Commands/UI/RegisterUIFragmentCommand.cs`
- **Class Name:** `RegisterUIFragmentCommand`
- **Implements:** `ICommand<string>`
- **Properties:**
    - `ParentId` (string?, read-only)
    - `SlotName` (string?, read-only)
    - `HtmlContent` (string, read-only)
    - `CssContent` (string?, read-only)
    - `JsContent` (string?, read-only)
    - `Priority` (HandlerPriority, read-only)
- **Constructor:**
    - `RegisterUIFragmentCommand(string htmlContent, string? parentId = null, string? slotName = null, string? cssContent = null, string? jsContent = null, HandlerPriority priority = HandlerPriority.Normal)`

### `Contract, UI & Routing/Core/Commands/UI/RequestUITemplateCommand.cs`
- **Class Name:** `RequestUITemplateCommand`
- **Implements:** `ICommand<string>`
- **Properties:**
    - `TemplateType` (string, read-only)
    - `ParentId` (string, read-only)
- **Constructor:**
    - `RequestUITemplateCommand(string templateType, string parentId)`

### `Contract, UI & Routing/Core/Commands/UI/SetElementPropertiesCommand.cs`
- **Class Name:** `SetElementPropertiesCommand`
- **Implements:** `ICommand`
- **Properties:**
    - `ElementId` (Guid, read-only)
    - `PropertiesToSet` (Dictionary<string, object>, read-only)
- **Constructor:**
    - `SetElementPropertiesCommand(Guid elementId, Dictionary<string, object> propertiesToSet)`

### `Contract, UI & Routing/Core/Commands/UI/ShareUIElementOwnershipCommand.cs`
- **Class Name:** `ShareUIElementOwnershipCommand`
- **Implements:** `ICommand`
- **Properties:**
    - `ElementId` (string, read-only)
    - `TargetModuleId` (string, read-only)
    - `Permissions` (AccessPermissions, read-only)
- **Constructor:**
    - `ShareUIElementOwnershipCommand(string elementId, string targetModuleId, AccessPermissions permissions)`

### `Contract, UI & Routing/Core/Commands/UI/UnregisterUIElementCommand.cs`
- **Class Name:** `UnregisterUIElementCommand`
- **Implements:** `ICommand`
- **Properties:**
    - `ElementId` (string, read-only)
- **Constructor:**
    - `UnregisterUIElementCommand(string elementId)`

### `Contract, UI & Routing/Core/Commands/UI/UpdateUIElementCommand.cs`
- **Class Name:** `UpdateUIElementCommand`
- **Implements:** `ICommand`
- **Properties:**
    - `ElementId` (string, read-only)
    - `HtmlContent` (string?, read-only)
    - `AttributesToSet` (Dictionary<string, string>?, read-only)
    - `AttributesToRemove` (List<string>?, read-only)
- **Constructor:**
    - `UpdateUIElementCommand(string elementId, string? htmlContent = null, Dictionary<string, string>? attributesToSet = null, List<string>? attributesToRemove = null)`

### `Contract, UI & Routing/Core/Data/AccessPermissions.cs`
- **Enum Name:** `AccessPermissions`
- **Members:** `None`, `Read`, `Write`, `Delete`, `Share`, `TransferOwnership`, `All`

### `Contract, UI & Routing/Core/Data/IDataAccessManager.cs`
- **Class Name:** `StoreItemMetadata`
    - **Properties:**
        - `OwnerModuleId` (string, get; set;)
        - `SharedPermissions` (Dictionary<string, AccessPermissions>, get; set;)
- **Interface Name:** `IDataAccessManager`
    - **Function:** `bool CheckPermission(StoreItemMetadata itemMetadata, string requestingModuleId, AccessPermissions requiredPermission)`
        - **Parameters:** `itemMetadata` (`StoreItemMetadata`), `requestingModuleId` (`string`), `requiredPermission` (`AccessPermissions`)
        - **Returns:** `bool`
    - **Function:** `bool IsOwner(StoreItemMetadata itemMetadata, string requestingModuleId)`
        - **Parameters:** `itemMetadata` (`StoreItemMetadata`), `requestingModuleId` (`string`)
        - **Returns:** `bool`
    - **Function:** `bool IsAdmin(string requestingModuleId)`
        - **Parameters:** `requestingModuleId` (`string`)
        - **Returns:** `bool`

### `Contract, UI & Routing/Core/Data/UIElement.cs`
- **Record Class Name:** `UIElement`
- **Properties (from record primary constructor):**
    - `Id` (string)
    - `OwnerId` (string)
    - `ElementType` (string)
    - `ParentId` (string?, optional)
    - `Attributes` (Dictionary<string, string>?, optional)
    - `Permissions` (AccessPermissions, optional, default `AccessPermissions.None`)

### `Contract, UI & Routing/Core/Data/UI/UIElement.cs`
- **Record Class Name:** `UIElement`
- **Properties (from record primary constructor):**
    - `Id` (string)
    - `OwnerId` (string)
    - `ElementType` (string)
    - `ParentId` (string?, optional)
    - `Attributes` (Dictionary<string, string>?, optional)
    - `Permissions` (AccessPermissions, optional, default `AccessPermissions.None`)

### `Contract, UI & Routing/Core/Diagnostics/ITraceContext.cs`
- **Interface Name:** `ITraceContext`
    - **Properties:**
        - `TraceId` (Guid, get)
        - `Items` (IDictionary<string, object>, get)
- **Class Name:** `TraceContext`
    - **Implements:** `ITraceContext`
    - **Properties:**
        - `TraceId` (Guid, get)
        - `Items` (IDictionary<string, object>, get)
    - **Constructor:**
        - `TraceContext()`

### `Contract, UI & Routing/Core/Discovery/CapabilityDescriptor.cs`
- **Class Name:** `CapabilityDescriptor`
- **Properties:**
    - `Name` (string, read-only)
    - `Description` (string, read-only)
    - `Type` (CapabilityType, read-only)
    - `Status` (CapabilityStatus, read-only)
    - `Priority` (int, read-only)
    - `ContractTypeName` (string, read-only)
    - `HandlerTypeName` (string, read-only)
    - `OwningModuleId` (string, read-only)
    - `Parameters` (IReadOnlyList<ParameterDescriptor>, read-only)
- **Constructor:**
    - `CapabilityDescriptor(string name, string description, CapabilityType type, CapabilityStatus status, int priority, string contractTypeName, string handlerTypeName, string owningModuleId, IReadOnlyList<ParameterDescriptor> parameters)`

### `Contract, UI & Routing/Core/Discovery/CapabilityStatus.cs`
- **Enum Name:** `CapabilityStatus`
- **Members:** `Available`, `MetadataMissing`, `Obsolete`, `Deprecated`, `Experimental`

### `Contract, UI & Routing/Core/Discovery/CapabilityType.cs`
- **Enum Name:** `CapabilityType`
- **Members:** `Command`, `Event`, `Job`, `ApiEndpoint`, `Service`

### `Contract, UI & Routing/Core/Discovery/ParameterDescriptor.cs`
- **Class Name:** `ParameterDescriptor`
- **Properties:**
    - `Name` (string, read-only)
    - `TypeName` (string, read-only)
    - `Description` (string, read-only)
- **Constructor:**
    - `ParameterDescriptor(string name, string typeName, string description = "")`

### `Contract, UI & Routing/Core/Discovery/RegistryManifest.cs`
- **Class Name:** `RegistryManifest`
- **Properties:**
    - `Capabilities` (IReadOnlyList<CapabilityDescriptor>, read-only)
    - `GeneratedAt` (System.DateTimeOffset, read-only)
- **Constructor:**
    - `RegistryManifest(IReadOnlyList<CapabilityDescriptor> capabilities)`

### `Contract, UI & Routing/Core/Discovery/Commands/GetRegistryManifestCommand.cs`
- **Class Name:** `GetRegistryManifestCommand`
- **Implements:** `ICommand<RegistryManifest>`

### `Contract, UI & Routing/Core/Errors/IErrorHandler.cs`
- **Interface Name:** `IErrorHandler`
    - **Function:** `Task HandleError(Exception exception, ITraceContext traceContext, string message = null, bool isCritical = false)`
        - **Parameters:** `exception` (`Exception`), `traceContext` (`ITraceContext`), `message` (`string`, optional), `isCritical` (`bool`, optional)
        - **Returns:** `Task`

### `Contract, UI & Routing/Core/Errors/ValidationException.cs`
- **Class Name:** `ValidationException`
- **Inherits from:** `Exception`
- **Constructors:**
    - `ValidationException(string message)`
    - `ValidationException(string message, Exception innerException)`

### `Contract, UI & Routing/Core/Events/IEvent.cs`
- **Interface Name:** `IEvent`
- **Interface Name:** `IEventHandler<in TEvent>` (where `TEvent : IEvent`)
    - **Function:** `Task Handle(TEvent @event, ITraceContext traceContext)`
        - **Parameters:** `@event` (`TEvent`), `traceContext` (`ITraceContext`)
        - **Returns:** `Task`

### `Contract, UI & Routing/Core/Events/UI/UIElementRegisteredEvent.cs`
- **Class Name:** `UIElementRegisteredEvent`
- **Implements:** `IEvent`
- **Properties:**
    - `NewElement` (UIElement, read-only)
    - `HtmlContent` (string, read-only)
    - `CssContent` (string?, read-only)
    - `JsContent` (string?, read-only)
- **Constructor:**
    - `UIElementRegisteredEvent(UIElement newElement, string htmlContent, string? cssContent = null, string? jsContent = null)`

### `Contract, UI & Routing/Core/Events/UI/UIElementUnregisteredEvent.cs`
- **Class Name:** `UIElementUnregisteredEvent`
- **Implements:** `IEvent`
- **Properties:**
    - `ElementId` (string, read-only)
- **Constructor:**
    - `UIElementUnregisteredEvent(string elementId)`

### `Contract, UI & Routing/Core/Events/UI/UIElementUpdatedEvent.cs`
- **Class Name:** `UIElementUpdatedEvent`
- **Implements:** `IEvent`
- **Properties:**
    - `ElementId` (string, read-only)
    - `UpdatedProperties` (Dictionary<string, object>, read-only)
- **Constructor:**
    - `UIElementUpdatedEvent(string elementId, Dictionary<string, object> updatedProperties)`

### `Contract, UI & Routing/Core/Events/UI/UIOwnershipChangedEvent.cs`
- **Class Name:** `UIOwnershipChangedEvent`
- **Implements:** `IEvent`
- **Properties:**
    - `ElementId` (string, read-only)
    - `NewAccessControl` (UIAccessControl, read-only)
- **Constructor:**
    - `UIOwnershipChangedEvent(string elementId, UIAccessControl newAccessControl)`

### `Contract, UI & Routing/Core/Jobs/IJob.cs`
- **Interface Name:** `IJob`
- **Properties:**
    - `CronExpression` (string, get)

### `Contract, UI & Routing/Core/Jobs/IJobHandler.cs`
- **Interface Name:** `IJobHandler<TJob>` (where `TJob : IJob`)
    - **Function:** `Task ExecuteAsync(TJob job, ITraceContext traceContext)`
        - **Parameters:** `job` (`TJob`), `traceContext` (`ITraceContext`)
        - **Returns:** `Task`

### `Contract, UI & Routing/Core/Logs/LogLevel.cs`
- **Enum Name:** `LogLevel`
- **Members:** `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`

### `Contract, UI & Routing/Core/Modules/IModule.cs`
- **Interface Name:** `IModule`
- **Properties:**
    - `Id` (string, get)
    - `Name` (string, get)
- **Functions:**
    - `void ConfigureServices(IServiceCollection services)`
        - **Parameters:** `services` (`IServiceCollection`)
        - **Returns:** `void`
    - `Task Initialize(IServiceProvider serviceProvider)`
        - **Parameters:** `serviceProvider` (`IServiceProvider`)
        - **Returns:** `Task`

### `Contract, UI & Routing/Core/Routing/IApiEndpoint.cs`
- **Interface Name:** `IApiEndpoint`
- **Properties:**
    - `Id` (string, get)
    - `HttpMethod` (string, get)
    - `Path` (string, get)
    - `Description` (string, get)
    - `OwningModuleId` (string, get)

### `Contract, UI & Routing/Core/UI/ElementType.cs`
- **Enum Name:** `ElementType`
- **Members:** `Panel`, `Button`, `Label`, `TextInput`, `Card`, `Fragment`

### `Contract, UI & Routing/Core/UI/ITemplateService.cs`
- **Interface Name:** `ITemplateService`
- **Functions:**
    - `Task<string> GetTemplateHtml(string templateType, Dictionary<string, object> parameters)`
        - **Parameters:** `templateType` (`string`), `parameters` (`Dictionary<string, object>`)
        - **Returns:** `Task<string>`

### `Contract, UI & Routing/Core/UI/UIAccessControl.cs`
- **Class Name:** `UIAccessControl`
- **Properties:**
    - `OwnerId` (string, get; set;)
    - `SharedAccess` (Dictionary<string, SoftwareCenter.Core.Data.AccessPermissions>, get; set;, initialized)

### `Contract, UI & Routing/Core/UI/UIElement.cs`
- **Class Name:** `UIElement`
- **Properties:**
    - `Id` (string, get; set;)
    - `ElementType` (ElementType, get; set;)
    - `OwnerModuleId` (string, get; set;)
    - `ParentId` (string, get; set;)
    - `Properties` (Dictionary<string, object>, get; set;, initialized)
    - `Children` (List<UIElement>, get; set;, initialized)
    - `Priority` (int, get; set;)
    - `SlotName` (string?, get; set;)
    - `AccessControl` (UIAccessControl, get; set;, initialized)