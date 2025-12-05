# SoftwareCenter.Kernel - Final Project Structure

This document outlines the final, verified structure of the `SoftwareCenter.Kernel` project. This project contains the concrete logic that implements the contracts defined in `SoftwareCenter.Core`. It is the "brain" of the application, responsible for module loading, command routing, and service orchestration.

## Project Files

- **SoftwareCenter.Kernel.csproj**: The .NET project file, defining dependencies like `SoftwareCenter.Core`.
- **StandardKernel.cs**: The primary concrete implementation of the `IKernel` interface. It aggregates all the internal services (router, event bus, etc.) and exposes them to modules.

## Internal System Commands
The `StandardKernel` pre-registers a set of internal commands essential for system diagnostics and discovery.
- **System.Help**: Returns a complete manifest of all registered commands in the system. This is the primary discovery mechanism for clients.
- **System.Log.Config**: Allows runtime configuration of system settings, such as toggling verbose logging.

## Namespaces & Files

### `SoftwareCenter.Kernel.Contexts`
This namespace contains components related to assembly loading and isolation.

- **/ModuleLoadContext.cs**: A custom `AssemblyLoadContext` that ensures each module is loaded into its own isolated memory space. This is critical for preventing DLL conflicts and enabling hot-swapping of modules.

### `SoftwareCenter.Kernel.Engine`
This namespace contains the core logic for module lifecycle management.

- **/ModuleLoader.cs**: Responsible for discovering module DLLs in the filesystem, loading them using the `ModuleLoadContext`, finding the `IModule` implementation, and triggering its initialization.

### `SoftwareCenter.Kernel.Routing`
This namespace provides the intelligent command routing and service discovery engine.

- **/HandlerRegistry.cs**: The concrete implementation of the service discovery catalog. It maintains a thread-safe, priority-sorted registry of all command handlers available in the system. Its `GetRegistryManifest()` method provides the full, detailed list of every handler to clients.
- **/SmartRouter.cs**: The concrete implementation of `IRouter`. It uses the `HandlerRegistry` to find the highest-priority handler for a given command. It also acts as a safety barrier, catching exceptions from modules, handling deprecated/obsolete commands, and injecting trace context.

### `SoftwareCenter.Kernel.Services`
This namespace contains concrete implementations of the various service contracts defined in `SoftwareCenter.Core`.

- **/DefaultEventBus.cs**: A standard, in-memory implementation of `IEventBus` for publish-subscribe messaging.
- **/GlobalDataStore.cs**: A default `IGlobalDataStore` implementation, likely using an in-memory dictionary or a simple file-based store for persistence.
- **/JobScheduler.cs**: A standard implementation of `IScheduler` for managing background jobs.
- **/KernelLogger.cs**: An internal logging service used by the Kernel itself.
