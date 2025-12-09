# Project: SoftwareCenter.UIManager

## Purpose
`SoftwareCenter.UIManager` (formerly UI.Engine) is the sole authority for all UI-related operations. It manages the state of the UI, processes requests to change the UI, and ensures that all UI elements are tracked and addressable. No other component manipulates the UI directly.

## Key Responsibilities
- **UI Command Processing:** Implement handlers for UI-specific commands (e.g., `CreateCardCommand`, `AddControlToContainerCommand`).
- **Element Management:** Generate and maintain a unique identifier for every UI element created (windows, containers, cards, buttons, etc.).
- **State Management:** Keep track of the current state of the UI, including which module owns which element.
- **Layout Definition:** Accept a base layout definition from the Host and populate it based on requests from modules.
- **Ownership:** Track ownership of UI elements, allowing owners to modify them or share ownership with other modules.

## Architectural Principles
- **Command-Driven:** All UI modifications are initiated by sending a command to the Kernel, which routes it to the UIManager.
- **No Direct Rendering:** The UIManager is framework-agnostic. It manages a model of the UI. The actual rendering is handled by the Host's frontend technology (e.g., Blazor) which binds to the state exposed by the UIManager.
- **Decoupled from Logic:** The UIManager does not know *why* a button is being added; it only knows how to add a button when it receives the appropriate command.
- **Referenced by Host:** The Host application initializes and holds a reference to the UIManager.

## Project Structure
```
SoftwareCenter.UIManager/
├── Commands/
│   ├── CreateCardCommand.cs
│   └── AddControlToContainerCommand.cs
├── Handlers/
│   ├── CreateCardCommandHandler.cs
│   └── AddControlToContainerCommandHandler.cs
├── Services/
│   └── UIStateService.cs
├── UIManagerServiceCollectionExtensions.cs
├── SoftwareCenter.UIManager.csproj
└── METADATA.md
```

## Dependencies
- `SoftwareCenter.Core`

## Detailed API/Component List

### `Contract, UI & Routing/UIManager/METADATA.MD`
- Project metadata file.

### `Contract, UI & Routing/UIManager/SoftwareCenter.UIManager.csproj`
- **Project References:**
    - `..\Core\SoftwareCenter.Core.csproj`

### `Contract, UI & Routing/UIManager/UIManagerServiceCollectionExtensions.cs`
- **Static Class Name:** `UIManagerServiceCollectionExtensions`
- **Extension Methods:**
    - `IServiceCollection AddUIManager(this IServiceCollection services)`
        - **Parameters:** `services` (`IServiceCollection`)
        - **Returns:** `IServiceCollection`

### `Contract, UI & Routing/UIManager/Handlers/CreateElementCommandHandler.cs`
- **Class Name:** `CreateElementCommandHandler`
- **Implements:** `ICommandHandler<CreateUIElementCommand, string>`
- **Constructor:**
    - `CreateElementCommandHandler(UIStateService uiStateService)`
- **Functions:**
    - `Task<string> Handle(CreateUIElementCommand command, ITraceContext traceContext)`
        - **Parameters:** `command` (`CreateUIElementCommand`), `traceContext` (`ITraceContext`)
        - **Returns:** `Task<string>`

### `Contract, UI & Routing/UIManager/Handlers/RegisterUIFragmentCommandHandler.cs`
- **Class Name:** `RegisterUIFragmentCommandHandler`
- **Implements:** `ICommandHandler<RegisterUIFragmentCommand, string>`
- **Constructor:**
    - `RegisterUIFragmentCommandHandler(UIStateService uiStateService)`
- **Functions:**
    - `Task<string> Handle(RegisterUIFragmentCommand command, ITraceContext traceContext)`
        - **Parameters:** `command` (`RegisterUIFragmentCommand`), `traceContext` (`ITraceContext`)
        - **Returns:** `Task<string>`

### `Contract, UI & Routing/UIManager/Handlers/RequestUITemplateCommandHandler.cs`
- **Class Name:** `RequestUITemplateCommandHandler`
- **Implements:** `ICommandHandler<RequestUITemplateCommand, string>`
- **Constructor:**
    - `RequestUITemplateCommandHandler(UIStateService uiStateService, ITemplateService templateService)`
- **Functions:**
    - `Task<string> Handle(RequestUITemplateCommand command, ITraceContext traceContext)`
        - **Parameters:** `command` (`RequestUITemplateCommand`), `traceContext` (`ITraceContext`)
        - **Returns:** `Task<string>`

### `Contract, UI & Routing/UIManager/Handlers/SetElementPropertiesCommandHandler.cs`
- **Class Name:** `SetElementPropertiesCommandHandler`
- **Implements:** `ICommandHandler<SetElementPropertiesCommand>`
- **Constructor:**
    - `SetElementPropertiesCommandHandler(UIStateService uiStateService)`
- **Functions:**
    - `Task Handle(SetElementPropertiesCommand command, ITraceContext traceContext)`
        - **Parameters:** `command` (`SetElementPropertiesCommand`), `traceContext` (`ITraceContext`)
        - **Returns:** `Task`

### `Contract, UI & Routing/UIManager/Handlers/ShareUIElementOwnershipCommandHandler.cs`
- **Class Name:** `ShareUIElementOwnershipCommandHandler`
- **Implements:** `ICommandHandler<ShareUIElementOwnershipCommand>`
- **Constructor:**
    - `ShareUIElementOwnershipCommandHandler(UIStateService uiStateService)`
- **Functions:**
    - `Task Handle(ShareUIElementOwnershipCommand command, ITraceContext traceContext)`
        - **Parameters:** `command` (`ShareUIElementOwnershipCommand`), `traceContext` (`ITraceContext`)
        - **Returns:** `Task`

### `Contract, UI & Routing/UIManager/Handlers/UnregisterUIElementCommandHandler.cs`
- **Class Name:** `UnregisterUIElementCommandHandler`
- **Implements:** `ICommandHandler<UnregisterUIElementCommand>`
- **Constructor:**
    - `UnregisterUIElementCommandHandler(UIStateService uiStateService)`
- **Functions:**
    - `Task Handle(UnregisterUIElementCommand command, ITraceContext traceContext)`
        - **Parameters:** `command` (`UnregisterUIElementCommand`), `traceContext` (`ITraceContext`)
        - **Returns:** `Task`

### `Contract, UI & Routing/UIManager/Handlers/UpdateUIElementCommandHandler.cs`
- **Class Name:** `UpdateUIElementCommandHandler`
- **Implements:** `ICommandHandler<UpdateUIElementCommand>`
- **Constructor:**
    - `UpdateUIElementCommandHandler(UIStateService uiStateService)`
- **Functions:**
    - `Task Handle(UpdateUIElementCommand command, ITraceContext traceContext)`
        - **Parameters:** `command` (`UpdateUIElementCommand`), `traceContext` (`ITraceContext`)
        - **Returns:** `Task`

### `Contract, UI & Routing/UIManager/Services/IUIHubNotifier.cs`
- **Interface Name:** `IUIHubNotifier`
- **Functions:**
    - `Task ElementAdded(object elementData)`
        - **Parameters:** `elementData` (`object`)
        - **Returns:** `Task`
    - `Task ElementUpdated(object elementData)`
        - **Parameters:** `elementData` (`object`)
        - **Returns:** `Task`
    - `Task ElementRemoved(object elementData)`
        - **Parameters:** `elementData` (`object`)
        - **Returns:** `Task`

### `Contract, UI & Routing/UIManager/Services/UIFragment.cs`
- **Class Name:** `UIFragment`
- **Properties:**
    - `Id` (string, get; set;)
    - `OwnerId` (string, get; set;)
    - `Priority` (HandlerPriority, get; set;)
    - `HtmlContent` (string, get; set;)
    - `SlotName` (string, get; set;)
    - `Element` (UIElement, get; set;)
    - `Attributes` (Dictionary<string, string>, get; set;, initialized)

### `Contract, UI & Routing/UIManager/Services/UIStateService.cs`
- **Class Name:** `UIStateService`
- **Constructor:**
    - `UIStateService(IEventBus eventBus)`
- **Functions:**
    - `UIElement CreateElement(string ownerId, ElementType elementType, string parentId, string htmlContent, string? cssContent = null, string? jsContent = null, int priority = 0, string? slotName = null, Dictionary<string, object>? properties = null, string? id = null)`
        - **Parameters:** `ownerId` (`string`), `elementType` (`ElementType`), `parentId` (`string`), `htmlContent` (`string`), `cssContent` (`string?`, optional), `jsContent` (`string?`, optional), `priority` (`int`, optional), `slotName` (`string?`, optional), `properties` (`Dictionary<string, object>?`, optional), `id` (`string?`, optional)
        - **Returns:** `UIElement`
    - `UIElement GetElement(string id)`
        - **Parameters:** `id` (`string`)
        - **Returns:** `UIElement`
    - `bool DeleteElement(string id)`
        - **Parameters:** `id` (`string`)
        - **Returns:** `bool`
    - `UIElement GetActiveElementForSlot(string slotName)`
        - **Parameters:** `slotName` (`string`)
        - **Returns:** `UIElement`
    - `IEnumerable<UIElement> GetAllElements()`
        - **Returns:** `IEnumerable<UIElement>`
    - `bool UpdateElement(string id, Dictionary<string, object> propertiesToUpdate)`
        - **Parameters:** `id` (`string`), `propertiesToUpdate` (`Dictionary<string, object>`)
        - **Returns:** `bool`
    - `bool SetAccessControl(string id, UIAccessControl newAccessControl)`
        - **Parameters:** `id` (`string`), `newAccessControl` (`UIAccessControl`)
        - **Returns:** `bool`
- **Nested Class Name:** `ElementPriorityComparer` (file-scoped internal class)
    - **Implements:** `IComparer<UIElement>`
    - **Functions:**
        - `int Compare(UIElement x, UIElement y)`
            - **Parameters:** `x` (`UIElement`), `y` (`UIElement`)
            - **Returns:** `int`

### `Contract, UI & Routing/UIManager/Validation/CreateElementCommandValidator.cs`
- **Class Name:** `CreateElementCommandValidator`
- **Implements:** `ICommandValidator<CreateElementCommand>`
- **Functions:**
    - `Task Validate(CreateElementCommand command, ITraceContext traceContext)`
        - **Parameters:** `command` (`CreateElementCommand`), `traceContext` (`ITraceContext`)
        - **Returns:** `Task`

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