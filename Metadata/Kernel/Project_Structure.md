# Project Structure: SoftwareCenter.Kernel

**Root**: `SoftwareCenterRefactored/Kernel/`

## File Tree
/Kernel
│
├── /Project Metadata
│   ├── Project_Structure.md
│   ├── Project_Implementation_Plan.md
│   └── Project_History_Log.md
│
├── /Routing (The Intelligence)
│   ├── SmartRouter.cs          # Implements IRouter
│   ├── HandlerRegistry.cs      # The Dynamic Catalog (ConcurrentDictionary)
│   └── CapabilityProvider.cs   # Exposes Registry items as RouteDefinitions
│
├── /Engine (Lifecycle)
│   ├── ModuleLoader.cs         # Handles AssemblyLoadContext
│
├── /Services
│   └── GlobalDataStore.cs      # LiteDB Implementation
│
├── /Contexts
│   └── ModuleLoadContext.cs    # Isolation
│
└── SoftwareCenter.Kernel.csproj


1. Architectural Overview: "Body & Brain"

The system is divided into two distinct lifecycle phases to ensure stability and extensibility.

Phase A: The Body (Host)

Role: The Physical Existence.

Responsibilities: * Starts the Process.

Creates the main Window (WPF/WinUI).

Provides "Dumb" I/O (Basic file read/write, native OS calls).

Logging: Provides a BasicFileLogger (Priority 0) that writes raw text to a local file.

Key Constraint: The Host knows nothing about business logic. It blindly executes what the Kernel tells it to.

Phase B: The Brain (Kernel)

Role: The Intelligence & Orchestrator (Backend for Frontend).

Responsibilities:

Traffic Cop: Intercepts all commands. Routing logic determines who executes it.

Registry: A dynamic catalog of every capability (Host + Modules).

Memory: Manages GlobalDataStore.db for persistent settings.

Safety: Wraps all execution in Exception Barriers and Tracing.

2. Core Projects

SoftwareCenter.Core (The Language)

Type: Class Library (.NET 8)

Content: * Contracts: IKernel, IRouter, IModule.

DTOs: CommandStatus, RouteDefinition, TraceContext.

Constants: Known Command IDs (System.Log, System.Init).

Rule: Contains NO logic. Only definitions used by both Host and Kernel.

SoftwareCenter.Kernel (The Brain)

Type: Class Library (.NET 8)

Dependencies: LiteDB, System.Runtime.Loader, Microsoft.Extensions.Logging.Abstractions.

Key Components:

SmartRouter: IRouter implementation with Trace Injection and Deprecation checks.

HandlerRegistry: Priority-based ConcurrentDictionary for Service Discovery.

ModuleLoader: AssemblyLoadContext manager for plugin isolation.

KernelLogger: Middleware that enriches logs with TraceContext before routing.

SoftwareCenter.Host (The Body)

Type: Windows Application (WPF/WinUI)

Dependencies: SoftwareCenter.Kernel, SoftwareCenter.Core.

Role: The entry point. Loads Kernel, hands over control, and waits for UI events.

3. Data Storage Strategy

Location: %AppData%/SoftwareCenter/

File: GlobalDataStore.db (LiteDB).

Purpose: * Settings: User preferences (Theme, VerboseLogging).

Cache: Temporary module data.

Future Proofing: Designed to coexist with future DB modules (e.g., DataManager) that might mount additional DB files.