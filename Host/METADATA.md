# Project: SoftwareCenter.Host

## Purpose
`SoftwareCenter.Host` is the main executable for the application. It configures and runs the Kestrel web server, initializes the `Kernel` and `UIManager`, and provides the default, baseline implementation for all required application services.

## Key Responsibilities
- **Application Entry Point:** Contains the `Program.main` method that starts the entire application.
- **Web Server Configuration:** Sets up and runs the ASP.NET Core Kestrel server.
- **System Initialization:** Is responsible for adding the `Kernel` and `UIManager` to the service collection and triggering the module loading process.
- **Default Service Provider:** Implements and registers basic, "good enough" versions of core services:
    - **Logging:** A simple logger that writes to a local text file.
    - **Installation:** A handler that executes the default OS action for a file (e.g., double-clicking an `.msi`).
    - **Repository:** A service that can list applications from a local folder.
- **Base UI Layout:** Sends the initial commands to the `UIManager` to construct the main application window, navigation, and content areas.

## Architectural Principles
- **Standalone Application:** The Host can run and provide core functionality without any modules present.
- **No Module References:** The Host has no compile-time knowledge of any specific module.
- **No Core Reference:** The Host does not reference `SoftwareCenter.Core` directly. It interacts with the system via the concrete `Kernel` and `UIManager` projects, which abstracts away the core contracts. This enforces a clean architectural boundary.
- **Extensible:** Its default service registrations can be overridden by modules that register themselves with a higher priority in the Kernel.

## Project Structure
```
SoftwareCenter.Host/
├── Services/
│   ├── DefaultFileLoggerProvider.cs
│   └── DefaultInstallCommandHandler.cs
├── wwwroot/
│   ├── index.html
│   └── ... (css, js)
├── Program.cs
├── SoftwareCenter.Host.csproj
└── METADATA.md
```

## Dependencies
- `SoftwareCenter.Kernel`
- `SoftwareCenter.UIManager`

## Detailed API/Component List

### `Host/InteractionRequest.cs`
- **Class Name:** `InteractionRequest`
- **Properties:**
    - `OwnerId` (string, get; set;)
    - `Action` (string, get; set;)
    - `AdditionalData` (Dictionary<string, object>, get; set;)

### `Host/Program.cs`
- **`WebApplication` builder setup:**
    - `builder.Services.AddKernel()`
    - `builder.Services.AddUIManager()`
    - `builder.Services.AddSignalR()`
    - `builder.Services.AddSingleton<ITemplateService, HostTemplateService>()`
- **API Endpoints:**
    - `app.MapPost("/api/dispatch/{commandName}", ...)`
        - **Endpoint:** `/api/dispatch/{commandName}`
        - **Method:** `POST`
        - **Parameters:** `commandName` (string, from route), `payload` (JsonElement, from body), `commandBus` (ICommandBus, from DI), `commandFactory` (CommandFactory, from DI)
        - **Returns:** `Results.NotFound`, `Results.BadRequest`, `Results.Ok`, `Results.Problem`
- **Frontend Hosting:**
    - `app.UseDefaultFiles()`
    - `app.UseStaticFiles()`
    - Dynamic `app.UseStaticFiles()` for module `wwwroot` directories.
    - `app.MapHub<UIHub>("/uihub")`
- **Host UI Initialization Logic:**
    - Creates `nav-button` for "Applications" using `RequestUITemplateCommand`.
    - Creates `content-zone` for "Applications" using `CreateUIElementCommand`.
    - Injects default HTML using `RegisterUIFragmentCommand`.

### `Host/SoftwareCenter.Host.csproj`
- **Project References:**
    - `..\Contract, UI & Routing\Kernel\SoftwareCenter.Kernel.csproj`
    - `..\Contract, UI & Routing\UIManager\SoftwareCenter.UIManager.csproj`

### `Host/UIHub.cs`
- **Class Name:** `UIHub`
- **Inherits from:** `Hub`

### `Host/Middleware/ErrorHandlingMiddleware.cs`
- **Class Name:** `ErrorHandlingMiddleware`
- **Constructor:**
    - `ErrorHandlingMiddleware(RequestDelegate next)`
- **Functions:**
    - `Task InvokeAsync(HttpContext context, IErrorHandler errorHandler)`
        - **Parameters:** `context` (`HttpContext`), `errorHandler` (`IErrorHandler`)
        - **Returns:** `Task`

### `Host/Services/HostLogger.cs`
- **Class Name:** `HostLogger`
- **Implements:** `IScLogger`, `IDisposable`
- **Properties:**
    - `Priority` (int, read-only)
- **Constructor:**
    - `HostLogger(string logDirectory)`
- **Functions:**
    - `void Log(LogEntry entry)`
        - **Parameters:** `entry` (`LogEntry`)
        - **Returns:** `void`
    - `void Dispose()`
        - **Returns:** `void`

### `Host/Services/HostTemplateService.cs`
- **Class Name:** `HostTemplateService`
- **Implements:** `ITemplateService`
- **Constructor:**
    - `HostTemplateService(IWebHostEnvironment webHostEnvironment)`
- **Functions:**
    - `Task<string> GetTemplateHtml(string templateType, Dictionary<string, object> parameters)`
        - **Parameters:** `templateType` (`string`), `parameters` (`Dictionary<string, object>`)
        - **Returns:** `Task<string>`
    - `string ProcessConditionalStatements(string html, Dictionary<string, object> parameters)` (private)
        - **Parameters:** `html` (`string`), `parameters` (`Dictionary<string, object>`)
        - **Returns:** `string`

### `Host/Services/UIHubNotifier.cs`
- **Class Name:** `UIHubNotifier`
- **Implements:** `IEventHandler<UIElementRegisteredEvent>`, `IEventHandler<UIElementUnregisteredEvent>`, `IEventHandler<UIElementUpdatedEvent>`
- **Constructor:**
    - `UIHubNotifier(IHubContext<UIHub> hubContext)`
- **Functions:**
    - `Task Handle(UIElementRegisteredEvent anEvent)`
        - **Parameters:** `anEvent` (`UIElementRegisteredEvent`)
        - **Returns:** `Task`
    - `Task Handle(UIElementUnregisteredEvent anEvent)`
        - **Parameters:** `anEvent` (`UIElementUnregisteredEvent`)
        - **Returns:** `Task`
    - `Task Handle(UIElementUpdatedEvent anEvent)`
        - **Parameters:** `anEvent` (`UIElementUpdatedEvent`)
        - **Returns:** `Task`

---

## UI Architecture & Flow (As of 2025-12-09)

### Core Concepts
The UI is a composite Single Page Application (SPA) built dynamically from UI fragments. The **`Host`** is the "SOMEBODY" that provides the static base UI framework (the shell `index.html`, main CSS, and main JS). The **`UIManager`** is the "SOMEBODY" that acts as the central service layer or "backend for the frontend," orchestrating the entire UI state and all dynamic modifications. Modules and the Host **do not** manipulate the UI directly; they send commands to the `UIManager` to request UI changes. All UI is provided by the Host and modules as raw HTML, CSS, and JS files, allowing for dynamic updates without recompilation.

### UI Composition and Flow
1.  **`Host` as Framework Provider**:
    *   The `Host` serves the main `index.html` which defines the core layout zones (e.g., Titlebar, Nav area, Content Container, Notification Flyout).
    *   It also provides the global `style.css` for the overall theme and a `script.js` for base interactions and to act as the client-side renderer for `UIManager` events.

2.  **`UIManager` as Orchestrator**:
    *   The `UIManager` exposes a command-based API for all UI manipulations. It maintains an internal state tree of all UI elements, their properties, ownership, and permissions.
    *   It receives requests from the `Host` and `Modules` to create, update, or delete UI elements.

3.  **Dynamic UI Registration**:
    *   **Host & Modules** register their UI fragments by sending commands to the `UIManager`. For example, `RegisterNavButtonCommand` or `RegisterContentContainerCommand`.
    *   The `UIManager` processes these requests, generates unique IDs for each element, and returns them to the requestor.
    *   **Priority & Overrides**: Modules can register UI with a higher priority. For example, a `SourceManager` module can override the default `Host`-provided source management UI. When the module is unloaded, the `UIManager` automatically reverts to the `Host`'s UI.

4.  **Container & Element Injection**:
    *   The `UIManager` provides templates for common UI structures, such as a "card" container.
    *   A module can request a card, receive its ID, and then send further commands to inject specific controls (text inputs, buttons, etc.) into that card. The `UIManager` ensures these elements are injected in a consistent order (e.g., top-to-bottom, left-to-right).

5.  **Ownership and Permissions**:
    *   The requestor of a UI element is its default **Owner**.
    *   Owners have full control and are responsible for handling interactions for their elements.
    *   Ownership and permissions (read/write/delete) can be partially or fully shared with other modules via specific commands, enabling collaborative UI scenarios.

6.  **Real-time Frontend Sync (via `Host`)**:
    *   After processing a UI command, the `UIManager` does not render HTML. Instead, it publishes an event (e.g., via a SignalR Hub in the `Host`) detailing the state change (e.g., `ElementAdded`, `AttributeUpdated`).
    *   The `Host`'s main `script.js` listens for these events and performs the necessary live DOM manipulations to reflect the new state, making the `UIManager` the single source of truth.

7.  **Module-Specific SPAs (Pop-out functionality)**:
    *   If a module registers its own full SPA (in addition to fragments), the `UIManager` will automatically add a "pop-out" button to that module's primary content container.
    *   Clicking this button will open the module's SPA in a new browser tab. Multiple SPAs per module are supported.

8.  **Theming and Constraints**:
    *   All UI elements, whether from templates or custom, are expected to inherit styling from the `Host`'s main theme to ensure a consistent look and feel. The `UIManager` can enforce certain constraints on dimensions and sizing. Scrollbars are managed on a per-container basis, never on the main SPA body.