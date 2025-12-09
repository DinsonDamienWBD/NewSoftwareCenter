# Project: SoftwareCenter.Kernel

## Purpose
`SoftwareCenter.Kernel` is the brain of the application. It acts as the central service broker, communication bus, and module manager. It is responsible for loading all components, wiring them together at runtime, and routing all internal communication.

## Key Responsibilities
- **Module Loading:** Discover, load, and initialize all `IModule` implementations from assemblies in the `Modules` directory.
- **Service Registry:** Maintain a runtime registry of all available services, their contracts, and their implementations.
- **Command & Event Bus:** Dispatch `ICommand` and `IEvent` objects to their registered handlers.
- **API Endpoint Registry:** Maintain a runtime registry of all discoverable API endpoints for routing and help generation.
- **Developer Help:** Provide a mechanism (e.g., a special command or API endpoint) for developers to query the registries to see what capabilities are currently available at runtime.
- **Pipeline Tracing:** Create and manage the `ITraceContext` for all operations, ensuring end-to-end traceability.

## Architectural Principles
- **Central Hub:** All inter-module and host-module communication flows through the Kernel.
- **No Business Logic:** The Kernel itself does not implement business logic (e.g., it doesn't know how to install an app). It only routes requests to the components that do.
- **Dependency Injection:** It heavily utilizes `Microsoft.Extensions.DependencyInjection` to manage dependencies and service lifetimes.
- **Referenced by Host:** The Host application initializes and holds a reference to the Kernel.

## Project Structure
```
SoftwareCenter.Kernel/
├── Services/
│   ├── CommandBus.cs
│   ├── EventBus.cs
│   ├── ModuleLoader.cs
│   └── ServiceRegistry.cs
├── KernelServiceCollectionExtensions.cs
├── SoftwareCenter.Kernel.csproj
└── METADATA.md
```

## Dependencies
- `SoftwareCenter.Core`

## Detailed API/Component List

### `Contract, UI & Routing/Kernel/KernelServiceCollectionExtensions.cs`
- **Static Class Name:** `KernelServiceCollectionExtensions`
- **Extension Methods:**
    - `IServiceCollection AddKernel(this IServiceCollection services)`
        - **Parameters:** `services` (`IServiceCollection`)
        - **Returns:** `IServiceCollection`
    - `Task UseKernel(this IServiceProvider serviceProvider)`
        - **Parameters:** `serviceProvider` (`IServiceProvider`)
        - **Returns:** `Task`

### `Contract, UI & Routing/Kernel/SimpleCronParser.cs`
- **Class Name:** `SimpleCronParser`
- **Constructor:**
    - `SimpleCronParser(string expression)`
- **Functions:**
    - `bool IsMatch(DateTimeOffset time)`
        - **Parameters:** `time` (`DateTimeOffset`)
        - **Returns:** `bool`
    - `bool MatchPart(string cronPart, int value)` (private)
        - **Parameters:** `cronPart` (`string`), `value` (`int`)
        - **Returns:** `bool`

### `Contract, UI & Routing/Kernel/SoftwareCenter.Kernel.csproj`
- **Project References:**
    - `..\Core\SoftwareCenter.Core.csproj`
- **Package References:**
    - `LiteDB` (Version 5.0.21)
    - `Microsoft.Extensions.Logging` (Version 8.0.0)

### `Contract, UI & Routing/Kernel/Data/AccessPermissions.cs`
- **Enum Name:** `AccessPermissions`
- **Members:** `None`, `Read`, `Write`, `Delete`, `Share`, `TransferOwnership`, `All`

### `Contract, UI & Routing/Kernel/Data/AuditRecord.cs`
- **Class Name:** `AuditRecord`
- **Properties:**
    - `Id` (Guid, get; set;)
    - `DataKey` (string, get; set;)
    - `OperationType` (string, get; set;)
    - `Timestamp` (DateTimeOffset, get; set;)
    - `TraceId` (Guid, get; set;)
    - `InitiatingModuleId` (string, get; set;)
    - `Context` (string, get; set;, initialized)

### `Contract, UI & Routing/Kernel/Data/StoreItem.cs`
- **Class Name:** `StoreItem<T>`
- **Properties:**
    - `Id` (Guid, get; set;)
    - `Key` (string, get; set;)
    - `Value` (T, get; set;)
    - `OwnerModuleId` (string, get; set;)
    - `SharedPermissions` (Dictionary<string, AccessPermissions>, get; set;, initialized)
    - `CreatorTraceId` (Guid, get; set;)
    - `CreatedAt` (DateTimeOffset, get; set;)
    - `LastUpdaterTraceId` (Guid, get; set;)
    - `LastUpdatedAt` (DateTimeOffset, get; set;)

### `Contract, UI & Routing/Kernel/Handlers/DefaultLogCommandHandler.cs`
- **Class Name:** `DefaultLogCommandHandler`
- **Implements:** `ICommandHandler<LogCommand>`
- **Attributes:** `[HandlerPriority(-100)]`
- **Constructor:**
    - `DefaultLogCommandHandler(ILogger<DefaultLogCommandHandler> logger)`
- **Functions:**
    - `Task Handle(LogCommand command, ITraceContext traceContext)`
        - **Parameters:** `command` (`LogCommand`), `traceContext` (`ITraceContext`)
        - **Returns:** `Task`
    - `Microsoft.Extensions.Logging.LogLevel ConvertCoreLogLevelToMsLogLevel(SoftwareCenter.Core.Logs.LogLevel coreLogLevel)` (private)
        - **Parameters:** `coreLogLevel` (`SoftwareCenter.Core.Logs.LogLevel`)
        - **Returns:** `Microsoft.Extensions.Logging.LogLevel`

### `Contract, UI & Routing/Kernel/Handlers/GetRegistryManifestCommandHandler.cs`
- **Class Name:** `GetRegistryManifestCommandHandler`
- **Implements:** `ICommandHandler<GetRegistryManifestCommand, RegistryManifest>`
- **Constructor:**
    - `GetRegistryManifestCommandHandler(RegistryManifestService manifestService)`
- **Functions:**
    - `Task<RegistryManifest> Handle(GetRegistryManifestCommand command, ITraceContext traceContext)`
        - **Parameters:** `command` (`GetRegistryManifestCommand`), `traceContext` (`ITraceContext`)
        - **Returns:** `Task<RegistryManifest>`

### `Contract, UI & Routing/Kernel/Models/ModuleInfo.cs`
- **Class Name:** `ModuleInfo`
- **Properties:**
    - `ModuleId` (string, read-only)
    - `Assembly` (Assembly, read-only)
    - `LoadContext` (ModuleLoadContext, read-only)
    - `Instance` (IModule, get; set;)
    - `State` (ModuleState, get; set;)
    - `Handlers` (List<ModuleLoader.DiscoveredHandler>, read-only, initialized)
    - `ApiEndpoints` (List<Type>, read-only, initialized)
    - `Services` (List<Type>, read-only, initialized)
- **Constructor:**
    - `ModuleInfo(string moduleId, Assembly assembly, ModuleLoadContext loadContext)`
- **Enum Name:** `ModuleState`
- **Members:** `Discovered`, `Loading`, `Loaded`, `Unloading`, `Unloaded`, `Error`

### `Contract, UI & Routing/Kernel/Services/CommandBus.cs`
- **Class Name:** `CommandBus`
- **Implements:** `ICommandBus`
- **Constructor:**
    - `CommandBus(ISmartCommandRouter router)`
- **Functions:**
    - `Task Dispatch(ICommand command, ITraceContext traceContext = null)`
        - **Parameters:** `command` (`ICommand`), `traceContext` (`ITraceContext`, optional)
        - **Returns:** `Task`
    - `Task<TResult> Dispatch<TResult>(ICommand<TResult> command, ITraceContext traceContext = null)`
        - **Parameters:** `command` (`ICommand<TResult>`), `traceContext` (`ITraceContext`, optional)
        - **Returns:** `Task<TResult>`

### `Contract, UI & Routing/Kernel/Services/CommandFactory.cs`
- **Class Name:** `CommandFactory`
- **Constructor:**
    - `CommandFactory(ModuleLoader moduleLoader)`
- **Functions:**
    - `Type GetCommandType(string commandName)`
        - **Parameters:** `commandName` (`string`)
        - **Returns:** `Type`

### `Contract, UI & Routing/Kernel/Services/DefaultDataAccessManager.cs`
- **Class Name:** `DefaultDataAccessManager`
- **Implements:** `IDataAccessManager`
- **Constants:** `AdminModuleId` (string, "Admin")
- **Functions:**
    - `bool CheckPermission(StoreItemMetadata itemMetadata, string requestingModuleId, AccessPermissions requiredPermission)`
        - **Parameters:** `itemMetadata` (`StoreItemMetadata`), `requestingModuleId` (`string`), `requiredPermission` (`AccessPermissions`)
        - **Returns:** `bool`
    - `bool IsOwner(StoreItemMetadata itemMetadata, string requestingModuleId)`
        - **Parameters:** `itemMetadata` (`StoreItemMetadata`), `requestingModuleId` (`string`)
        - **Returns:** `bool`
    - `bool IsAdmin(string requestingModuleId)`
        - **Parameters:** `requestingModuleId` (`string`)
        - **Returns:** `bool`

### `Contract, UI & Routing/Kernel/Services/DefaultErrorHandler.cs`
- **Class Name:** `DefaultErrorHandler`
- **Implements:** `IErrorHandler`
- **Constructor:**
    - `DefaultErrorHandler(ICommandBus commandBus)`
- **Functions:**
    - `Task HandleError(Exception exception, ITraceContext traceContext, string message = null, bool isCritical = false)`
        - **Parameters:** `exception` (`Exception`), `traceContext` (`ITraceContext`), `message` (`string`, optional), `isCritical` (`bool`, optional)
        - **Returns:** `Task`

### `Contract, UI & Routing/Kernel/Services/EventBus.cs`
- **Class Name:** `EventBus`
- **Implements:** `IEventBus`
- **Constructor:**
    - `EventBus(IServiceProvider serviceProvider)`
- **Functions:**
    - `Task Publish(IEvent @event, ITraceContext traceContext = null)`
        - **Parameters:** `@event` (`IEvent`), `traceContext` (`ITraceContext`, optional)
        - **Returns:** `Task`

### `Contract, UI & Routing/Kernel/Services/GlobalDataStore.cs`
- **Class Name:** `GlobalDataStore`
- **Implements:** `IDisposable`
- **Constructor:**
    - `GlobalDataStore(string databasePath = "global_store.db", IDataAccessManager dataAccessManager = null)`
- **Functions:**
    - `T Get<T>(string key, ITraceContext traceContext)`
        - **Parameters:** `key` (`string`), `traceContext` (`ITraceContext`)
        - **Returns:** `T`
    - `void Set<T>(string key, T value, string ownerModuleId, ITraceContext traceContext, AccessPermissions initialSharedPermissions = AccessPermissions.None)`
        - **Parameters:** `key` (`string`), `value` (`T`), `ownerModuleId` (`string`), `traceContext` (`ITraceContext`), `initialSharedPermissions` (`AccessPermissions`, optional)
        - **Returns:** `void`
    - `void Delete(string key, ITraceContext traceContext)`
        - **Parameters:** `key` (`string`), `traceContext` (`ITraceContext`)
        - **Returns:** `void`
    - `void ShareData(string key, string targetModuleId, AccessPermissions permissions, ITraceContext traceContext)`
        - **Parameters:** `key` (`string`), `targetModuleId` (`string`), `permissions` (`AccessPermissions`), `traceContext` (`ITraceContext`)
        - **Returns:** `void`
    - `void TransferOwnership(string key, string newOwnerModuleId, ITraceContext traceContext)`
        - **Parameters:** `key` (`string`), `newOwnerModuleId` (`string`), `traceContext` (`ITraceContext`)
        - **Returns:** `void`
    - `string GetInitiatingModuleId(ITraceContext traceContext)` (private)
        - **Parameters:** `traceContext` (`ITraceContext`)
        - **Returns:** `string`
    - `void LogAudit(string dataKey, string operationType, ITraceContext traceContext, object context = null)` (private)
        - **Parameters:** `dataKey` (`string`), `operationType` (`string`), `traceContext` (`ITraceContext`), `context` (`object`, optional)
        - **Returns:** `void`
    - `void Dispose()`
        - **Returns:** `void`

### `Contract, UI & Routing/Kernel/Services/ICommandBus.cs`
- **Interface Name:** `ICommandBus`
- **Functions:**
    - `Task Dispatch(ICommand command, ITraceContext traceContext = null)`
        - **Parameters:** `command` (`ICommand`), `traceContext` (`ITraceContext`, optional)
        - **Returns:** `Task`
    - `Task<TResult> Dispatch<TResult>(ICommand<TResult> command, ITraceContext traceContext = null)`
        - **Parameters:** `command` (`ICommand<TResult>`), `traceContext` (`ITraceContext`, optional)
        - **Returns:** `Task<TResult>`

### `Contract, UI & Routing/Kernel/Services/IEventBus.cs`
- **Interface Name:** `IEventBus`
- **Functions:**
    - `Task Publish(IEvent @event, ITraceContext traceContext = null)`
        - **Parameters:** `@event` (`IEvent`), `traceContext` (`ITraceContext`, optional)
        - **Returns:** `Task`

### `Contract, UI & Routing/Kernel/Services/IServiceRegistry.cs`
- **Interface Name:** `IServiceRegistry`
- **Functions:**
    - `void Register<TService, TImplementation>(string owningModuleId)`
        - **Parameters:** `owningModuleId` (`string`)
        - **Returns:** `void`
    - `Type Get<TService>()`
        - **Returns:** `Type`
    - `void UnregisterModuleServices(string moduleId)`
        - **Parameters:** `moduleId` (`string`)
        - **Returns:** `void`

### `Contract, UI & Routing/Kernel/Services/IServiceRoutingRegistry.cs`
- **Interface Name:** `IServiceRoutingRegistry`
- **Functions:**
    - `void RegisterHandler(Type contractType, Type handlerType, Type handlerInterfaceType, int priority, string owningModuleId)`
        - **Parameters:** `contractType` (`Type`), `handlerType` (`Type`), `handlerInterfaceType` (`Type`), `priority` (`int`), `owningModuleId` (`string`)
        - **Returns:** `void`
    - `HandlerRegistration GetHighestPriorityHandler(Type contractType)`
        - **Parameters:** `contractType` (`Type`)
        - **Returns:** `HandlerRegistration`
    - `IEnumerable<HandlerRegistration> GetAllHandlers(Type contractType)`
        - **Parameters:** `contractType` (`Type`)
        - **Returns:** `IEnumerable<HandlerRegistration>`
    - `void UnregisterModuleHandlers(string moduleId)`
        - **Parameters:** `moduleId` (`string`)
        - **Returns:** `void`
- **Class Name:** `HandlerRegistration`
- **Properties:**
    - `ContractType` (Type, read-only)
    - `HandlerType` (Type, read-only)
    - `HandlerInterfaceType` (Type, read-only)
    - `Priority` (int, read-only)
    - `OwningModuleId` (string, read-only)
- **Constructor:**
    - `HandlerRegistration(Type contractType, Type handlerType, Type handlerInterfaceType, int priority, string owningModuleId)`

### `Contract, UI & Routing/Kernel/Services/ISmartCommandRouter.cs`
- **Interface Name:** `ISmartCommandRouter`
- **Functions:**
    - `Task Route(ICommand command, ITraceContext traceContext)`
        - **Parameters:** `command` (`ICommand`), `traceContext` (`ITraceContext`)
        - **Returns:** `Task`
    - `Task<TResult> Route<TResult>(ICommand<TResult> command, ITraceContext traceContext)`
        - **Parameters:** `command` (`ICommand<TResult>`), `traceContext` (`ITraceContext`)
        - **Returns:** `Task<TResult>`

### `Contract, UI & Routing/Kernel/Services/JobSchedulerService.cs`
- **Class Name:** `JobSchedulerService`
- **Implements:** `BackgroundService`
- **Constructor:**
    - `JobSchedulerService(IServiceProvider serviceProvider, ModuleLoader moduleLoader, ILogger<JobSchedulerService> logger, IErrorHandler errorHandler)`
- **Functions:**
    - `protected override Task ExecuteAsync(CancellationToken stoppingToken)`
        - **Parameters:** `stoppingToken` (`CancellationToken`)
        - **Returns:** `Task`
    - `void DiscoverJobs()` (private)
        - **Returns:** `void`
    - `void RegisterJob(IJob jobInstance, Type jobType)`
        - **Parameters:** `jobInstance` (`IJob`), `jobType` (`Type`)
        - **Returns:** `void`
    - `void DeregisterJob(Type jobType)`
        - **Parameters:** `jobType` (`Type`)
        - **Returns:** `void`
    - `Task ExecuteJobAsync(JobRunner runner, CancellationToken token)` (private)
        - **Parameters:** `runner` (`JobRunner`), `token` (`CancellationToken`)
        - **Returns:** `Task`
- **Nested Class Name:** `JobRunner`
    - **Properties:**
        - `JobInstance` (IJob, read-only)
        - `JobType` (Type, read-only)
        - `LastRun` (DateTimeOffset, get; private set;)
        - `IsRunning` (bool, get; private set;)
    - **Constructor:**
        - `JobRunner(IJob job, Type type)`
    - **Functions:**
        - `bool ShouldRun(DateTimeOffset now)`
            - **Parameters:** `now` (`DateTimeOffset`)
            - **Returns:** `bool`
        - `void MarkRunning()`
            - **Returns:** `void`
        - `void MarkComplete()`
            - **Returns:** `void`

### `Contract, UI & Routing/Kernel/Services/ModuleLoadContext.cs`
- **Class Name:** `ModuleLoadContext`
- **Inherits from:** `AssemblyLoadContext`
- **Constructor:**
    - `ModuleLoadContext(string modulePath)`
- **Functions:**
    - `protected override Assembly Load(AssemblyName assemblyName)`
        - **Parameters:** `assemblyName` (`AssemblyName`)
        - **Returns:** `Assembly`
    - `protected override nint LoadUnmanagedDll(string unmanagedDllName)`
        - **Parameters:** `unmanagedDllName` (`string`)
        - **Returns:** `nint`

### `Contract, UI & Routing/Kernel/Services/ModuleLoader.cs`
- **Class Name:** `ModuleLoader`
- **Constructor:**
    - `ModuleLoader(IErrorHandler errorHandler, IServiceRoutingRegistry serviceRoutingRegistry, IServiceRegistry serviceRegistry)`
- **Nested Class Name:** `DiscoveredHandler`
    - **Properties:**
        - `HandlerType` (Type, get; set;)
        - `ContractType` (Type, get; set;)
        - `InterfaceType` (Type, get; set;)
        - `Priority` (int, get; set;)
        - `OwningModuleId` (string, get; set;)
- **Functions:**
    - `void LoadModulesFromDisk()`
        - **Returns:** `void`
    - `Assembly LoadModule(string dllPath)`
        - **Parameters:** `dllPath` (`string`)
        - **Returns:** `Assembly`
    - `void UnloadModule(string moduleId)`
        - **Parameters:** `moduleId` (`string`)
        - **Returns:** `void`
    - `void DiscoverModuleCapabilities(ModuleInfo moduleInfo)` (private)
        - **Parameters:** `moduleInfo` (`ModuleInfo`)
        - **Returns:** `void`
    - `List<Assembly> GetLoadedAssemblies()`
        - **Returns:** `List<Assembly>`
    - `IEnumerable<ModuleInfo> GetLoadedModules()`
        - **Returns:** `IEnumerable<ModuleInfo>`
    - `List<DiscoveredHandler> GetDiscoveredHandlers()`
        - **Returns:** `List<DiscoveredHandler>`
    - `List<IApiEndpoint> GetDiscoveredApiEndpoints()`
        - **Returns:** `List<IApiEndpoint>`
    - `List<Type> GetDiscoveredModuleTypes()`
        - **Returns:** `List<Type>`

### `Contract, UI & Routing/Kernel/Services/RegistryManifestService.cs`
- **Class Name:** `RegistryManifestService`
- **Constructor:**
    - `RegistryManifestService(ModuleLoader moduleLoader)`
- **Functions:**
    - `RegistryManifest GenerateManifest()`
        - **Returns:** `RegistryManifest`

### `Contract, UI & Routing/Kernel/Services/ServiceRegistry.cs`
- **Class Name:** `ServiceRegistry`
- **Implements:** `IServiceRegistry`
- **Nested Class Name:** `ServiceRegistration`
    - **Properties:**
        - `ServiceType` (Type, read-only)
        - `ImplementationType` (Type, read-only)
        - `OwningModuleId` (string, read-only)
    - **Constructor:**
        - `ServiceRegistration(Type serviceType, Type implementationType, string owningModuleId)`
- **Functions:**
    - `void Register<TService, TImplementation>(string owningModuleId)`
        - **Parameters:** `owningModuleId` (`string`)
        - **Returns:** `void`
    - `Type Get<TService>()`
        - **Returns:** `Type`
    - `void UnregisterModuleServices(string moduleId)`
        - **Parameters:** `moduleId` (`string`)
        - **Returns:** `void`

### `Contract, UI & Routing/Kernel/Services/ServiceRoutingRegistry.cs`
- **Class Name:** `ServiceRoutingRegistry`
- **Implements:** `IServiceRoutingRegistry`
- **Functions:**
    - `void RegisterHandler(Type contractType, Type handlerType, Type handlerInterfaceType, int priority, string owningModuleId)`
        - **Parameters:** `contractType` (`Type`), `handlerType` (`Type`), `handlerInterfaceType` (`Type`), `priority` (`int`), `owningModuleId` (`string`)
        - **Returns:** `void`
    - `HandlerRegistration GetHighestPriorityHandler(Type contractType)`
        - **Parameters:** `contractType` (`Type`)
        - **Returns:** `HandlerRegistration`
    - `IEnumerable<HandlerRegistration> GetAllHandlers(Type contractType)`
        - **Parameters:** `contractType` (`Type`)
        - **Returns:** `IEnumerable<HandlerRegistration>`
    - `void UnregisterModuleHandlers(string moduleId)`
        - **Parameters:** `moduleId` (`string`)
        - **Returns:** `void`
- **Nested Class Name:** `HandlerRegistrationComparer`
    - **Implements:** `IComparer<HandlerRegistration>`
    - **Functions:**
        - `int Compare(HandlerRegistration x, HandlerRegistration y)`
            - **Parameters:** `x` (`HandlerRegistration`), `y` (`HandlerRegistration`)
            - **Returns:** `int`

### `Contract, UI & Routing/Kernel/Services/SmartCommandRouter.cs`
- **Class Name:** `SmartCommandRouter`
- **Implements:** `ISmartCommandRouter`
- **Constructor:**
    - `SmartCommandRouter(IServiceProvider serviceProvider, IServiceRoutingRegistry routingRegistry, ILogger<SmartCommandRouter> logger, IErrorHandler errorHandler)`
- **Functions:**
    - `Task Route(ICommand command, ITraceContext traceContext)`
        - **Parameters:** `command` (`ICommand`), `traceContext` (`ITraceContext`)
        - **Returns:** `Task`
    - `Task<TResult> Route<TResult>(ICommand<TResult> command, ITraceContext traceContext)`
        - **Parameters:** `command` (`ICommand<TResult>`), `traceContext` (`ITraceContext`)
        - **Returns:** `Task<TResult>`
    - `Task ValidateCommand(ICommand command, ITraceContext traceContext)` (private)
        - **Parameters:** `command` (`ICommand`), `traceContext` (`ITraceContext`)
        - **Returns:** `Task`
    - `Task RouteAndExecute(ICommand command, ITraceContext traceContext, List<HandlerRegistration> candidateHandlers)` (private)
        - **Parameters:** `command` (`ICommand`), `traceContext` (`ITraceContext`), `candidateHandlers` (`List<HandlerRegistration>`)
        - **Returns:** `Task`
    - `Task<TResult> RouteAndExecute<TResult>(ICommand<TResult> command, ITraceContext traceContext, List<HandlerRegistration> candidateHandlers)` (private)
        - **Parameters:** `command` (`ICommand<TResult>`), `traceContext` (`ITraceContext`), `candidateHandlers` (`List<HandlerRegistration>`)
        - **Returns:** `Task<TResult>`

### `Contract, UI & Routing/Kernel/Services/XmlDocumentationParser.cs`
- **Class Name:** `XmlDocumentationParser`
- **Constructor:**
    - `XmlDocumentationParser(XDocument xmlDoc)` (private)
- **Static Functions:**
    - `static bool TryCreateForAssembly(Assembly assembly, out XmlDocumentationParser parser)`
        - **Parameters:** `assembly` (`Assembly`), `parser` (`out XmlDocumentationParser`)
        - **Returns:** `bool`
- **Functions:**
    - `string GetTypeSummary(Type type)`
        - **Parameters:** `type` (`Type`)
        - **Returns:** `string`
    - `List<ParameterDescriptor> GetConstructorParameters(ConstructorInfo ctor)`
        - **Parameters:** `ctor` (`ConstructorInfo`)
        - **Returns:** `List<ParameterDescriptor>`