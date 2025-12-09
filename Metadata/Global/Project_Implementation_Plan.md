# UI Framework Implementation Plan

This document outlines the step-by-step plan to implement the dynamic, composite UI framework as described in the `Host` and `UIManager` metadata files.

## Phase 1: Core Contract and Model Definition

**Location:** `Contract, UI & Routing/Core`

1.  **Define UI Element Models:**
    *   Create `SoftwareCenter.Core.UI.UIElement.cs`: A base class or interface representing any UI element. It will contain properties like `Id` (string), `OwnerId` (string), `ParentId` (string), `ElementType` (enum: e.g., `NavButton`, `ContentContainer`, `Card`, `Button`, `Label`).
    *   Create `SoftwareCenter.Core.UI.UIAccessControl.cs`: A class defining ownership and permissions (e.g., `OwnerId`, `SharedWith` list with specific permissions).

2.  **Define UI Commands:**
    *   Create command records in `SoftwareCenter.Core.Commands` for all UI operations. These commands will be the primary way modules interact with the `UIManager`.
    *   `RegisterUIElementCommand(string RequesterId, ElementType Type, string ParentId, Dictionary<string, string> Attributes)`
    *   `UnregisterUIElementCommand(string RequesterId, string ElementId)`
    *   `UpdateUIElementCommand(string RequesterId, string ElementId, Dictionary<string, string> AttributesToUpdate)`
    *   `SetUIElementOwnershipCommand(string RequesterId, string ElementId, UIAccessControl NewAccessControl)`
    *   `RequestUIFromTemplateCommand(string RequesterId, string TemplateName, string ParentId, Dictionary<string, object> Data)`

3.  **Define UI Events:**
    *   Create event records in `SoftwareCenter.Core.Events` that the `UIManager` will raise after processing a command. The `Host` will listen for these events.
    *   `UIElementRegisteredEvent(UIElement NewElement, string? RawHtmlTemplate)`
    *   `UIElementUnregisteredEvent(string ElementId)`
    *   `UIElementUpdatedEvent(string ElementId, Dictionary<string, string> UpdatedAttributes)`
    *   `UIOwnershipChangedEvent(string ElementId, UIAccessControl NewAccessControl)`

## Phase 2: Host - The Base UI Framework

**Location:** `Host/wwwroot` & `Host/Services`

1.  **Create Base HTML Structure (`index.html`):**
    *   Define the main layout with `div`s for the primary zones:
        *   `<div id="titlebar-zone"></div>`
        *   `<div id="nav-zone"></div>`
        *   `<div id="content-zone"></div>`
        *   `<div id="notification-zone"></div>`
        *   `<div id="power-zone"></div>`

2.  **Create Base CSS (`Css/style.css`):**
    *   Define the overall theme, color palette, fonts, and basic styles for standard HTML elements.
    *   Include styles for the layout zones.
    *   Define a class for "cards" or other small containers.
    *   Ensure no scrollbar on the `body` element.

3.  **Create Client-Side Logic (`Js/script.js`):**
    *   Establish a SignalR connection to the `UIHub`.
    *   Implement client-side handlers for the UI events defined in Phase 1 (e.g., `onUIElementRegistered`, `onUIElementUnregistered`).
    *   These handlers will perform the actual DOM manipulation (e.g., `document.getElementById(parentId).innerHTML += newElementHtml;`).
    *   Implement logic for base interactions, like showing/hiding content containers when a nav button is clicked. A class like `.content-container.active` can be used.

4.  **Create UI Templates:**
    *   In a new `Host/wwwroot/templates` directory, create raw HTML files for UI elements.
    *   `nav-button.html`: `<button id="{{Id}}" class="nav-button">{{Label}}</button>`
    *   `content-container.html`: `<div id="{{Id}}" class="content-container"></div>`
    *   `card.html`: `<div id="{{Id}}" class="card"><div class="card-header">{{Title}}</div><div class="card-body"></div></div>`
    *   These templates will use a simple placeholder syntax like `{{PlaceholderName}}`.

5.  **Create `UIHubNotifier` Service (`Host/Services/UIHubNotifier.cs`):**
    *   This service will subscribe to the UI events from the `Core` event bus.
    *   Upon receiving an event (e.g., `UIElementRegisteredEvent`), it will forward a corresponding message to the connected clients via the `UIHub`. It acts as the bridge between the .NET event system and the client-side SignalR listeners.

## Phase 3: UIManager - The Orchestrator

**Location:** `Contract, UI & Routing/UIManager`

1.  **Implement `UIManagerService` (`Services/UIManagerService.cs`):**
    *   Create a central service to manage the UI state.
    *   It will hold a concurrent dictionary to store the state of all UI elements: `ConcurrentDictionary<string, UIElement>`.
    *   It will also need a way to access the UI templates from the `Host` (e.g., via file access, assuming it knows the `wwwroot` path).

2.  **Implement Command Handlers (`Handlers/`):**
    *   For each command from Phase 1, create a corresponding handler.
    *   **`RegisterUIElementCommandHandler`:**
        *   Generate a unique ID for the new element.
        *   Create the `UIElement` model.
        *   Store it in the `UIManagerService`'s dictionary.
        *   If it's based on a template, read the template HTML from `Host/wwwroot/templates`.
        *   Perform simple string replacement for placeholders (`{{Id}}`, `{{Label}}`, etc.).
        *   Publish a `UIElementRegisteredEvent` with the `UIElement` data and the generated HTML.
    *   **`UnregisterUIElementCommandHandler`:**
        *   Remove the element from the dictionary.
        *   Publish a `UIElementUnregisteredEvent`.
    *   Implement other handlers following the same pattern.

3.  **Implement Override Logic:**
    *   When a registration request comes in, the handler will check if an element with the same functional purpose (e.g., a nav button for "Source Management") already exists.
    *   It will use a priority system (e.g., an optional `Priority` property in the registration command, with Modules having higher priority than Host) to decide whether to hide the existing element and show the new one.
    *   When a higher-priority element is unregistered, the `UIManager` should automatically show the lower-priority element it replaced.

## Phase 4: Integration and Verification

1.  **Host Startup Integration (`Host/Program.cs`):**
    *   On startup, the `Host` will send a series of `RegisterUIElementCommand`s to the `UIManager` to build its own base UI (nav buttons, content containers, etc.).

2.  **Module Example:**
    *   Create a simple test module that registers a nav button and a content container with a card.
    *   The module will read a `ui.json` file to define its UI request.
    *   On startup, it will deserialize the JSON and send the appropriate commands to the `UIManager`.

3.  **Testing:**
    *   Launch the `Host` application.
    *   Verify that the base UI appears correctly.
    *   Verify that the test module's UI appears and overrides any `Host` UI if applicable.
    *   Use the browser's developer tools to inspect the DOM and watch the SignalR messages.
    *   Test unregistering the module and verify the UI reverts correctly.
