# Mandatory Project Requirements (Must-Haves)

This file tracks foundational requirements and assigns responsibility to a specific project, ensuring all critical services are guaranteed by at least one module.

## F-REQ-001: Guaranteed Basic Logging Service

| Attribute | Value |
| :--- | :--- |
| **Description** | A basic, highly-available service for recording logs, warnings, and errors. It must be present even if specialized logging modules are not loaded. |
| **API Contract** | Must support the `RecordLogOrNotification` command action via the `System.Log` target. |
| **Module ID** | `Host.Logging` |
| **Responsibility** | `Host/` Project |
| **Status** | PENDING (Host Implementation) |

## F-REQ-002: Service Overriding Mechanism

| Attribute | Value |
| :--- | :--- |
| **Description** | The Smart Router must dynamically route foundational service commands (like `System.Log`) to a higher-priority, specialized module if one is registered. |
| **API Contract** | Handled by `IServiceRegistry.GetHighestPriorityModuleId()`. |
| **Responsibility** | `Kernel/` Project (Implemented in `DefaultSmartRouter`) |
| **Status** | COMPLETE (Kernel Routing Logic) |

## F-REQ-003. System Architecture: "Body & Brain" Model

| The system uses a hierarchical "Override" architecture to ensure stability while enabling advanced features. |
|  |
| ### A. SoftwareCenter.Host (The Body) |
| * **Role:** Provides the physical existence (Window, Process), basic reflexes, and native I/O. |
| * **Capabilities:** |
|     * **BasicRouter:** A primitive router handling file I/O, window management, and local caching. |
|     * **Native Services:** File system access, OS integration, local encyrpted settings. |
| * **Lifecycle:** Starts first. Loads Core. Loads Kernel. |
|  |
| ### B. SoftwareCenter.Kernel (The Brain) |
| * **Role:** The intelligence engine. Once loaded, it **takes over** the system. |
| * **Responsibilities:** |
|     * **Router Override:** Replaces the Host's `BasicRouter` with `SmartRouter`. All commands (even file I/O) are routed through the Kernel. |
|     * **Dynamic Registry:** A living catalog of all available commands (Host + Modules) with rich metadata (Description, Version, Status). |
|     * **Global Memory:** Hosts the `GlobalDataStore` (LiteDB), a high-speed NoSQL store accessible to Modules and Host for shared complex data. |
|     * **Module Loader:** Manages the lifecycle (Load/Unload/Isolation) of external plugins. |
|  |
| ### C. SoftwareCenter.Core (The Language) |
| * **Role:** Static contracts and definitions. |
| * **Content:** Interfaces (`IRouter`, `IModule`), Enums (`CommandStatus`), and DTOs (`RouteDefinition`). It contains **no logic**, only definitions. |