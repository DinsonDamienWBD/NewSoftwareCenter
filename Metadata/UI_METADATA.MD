# Software Center - UI Architecture Metadata
**Version:** 2.0 (Composition Engine)
**Pattern:** Hybrid Composite UI (Server-Shell + Client-Hydration)
**Technology:** ASP.NET Core Razor (Host) + Vanilla JS (Client) + SignalR (Transport)

---

This document details how the User Interface is composed, rendered, and updated.
This project utilizes a **Composite UI (Micro-Frontend)** architecture. The "Host" application provides the shell, layout, and styling, while various "Modules" provide functional UI fragments.

The composition logic is handled by the backend **FrontendManager** project, which orchestrates the injection of HTML fragments into specific slots (Zones) within the Host, ensuring a unified look and feel via a centralized Template Library. The core logic for this composition is entirely implemented within the FrontendManager project.

## 1. Overview: The "Mosaic" Strategy
The UI is not a monolithic page. It is a mosaic of fragments.
* **Zones:** Named slots in the layout (e.g., `NavRail`, `MainContent`, `StatusBar`).
* **Zone Files:** Zones must be wrapped in a container that defines their identity.
```
html
<div class="zone-wrapper" data-GUID="{{UI_COMPONENT_ID}}">
    <div id="app-title">Software Center</div>
    
    <div id="titlebar-controls" data-mount-point="default"></div>
</div>
```
* **Fragments:** HTML chunks injected into Zones.
* **Ownership:** Every pixel is owned by a specific Module via a `GUID`.

---

## 2. Core Concepts
### 2.1. Identity vs. Capacity
Every UI component (Zone or Fragment) is defined by two primary attributes:
1.  **Identity**
    **`data-GUID`(The Global Address):**
    * **Definition:** A unique UUID assigned *at runtime* by the FrontendManager.
    * **Purpose:** Allows the backend to target *this specific instance* of a component.
    * **Example:** `123e4567-e89b-12d3-a456-426614174000`
    
    **`data-ui-key`(The Local Address):**
    * **Definition:** A static string name defined in the Template.
    * **Purpose:** Allows Module JS to find buttons *inside* their specific instance.
    * **Example:** `btn-save`, `lbl-status`.
    * **Scope:** Unique only within the specific template instance.
    * **Usage Pattern:**
    * Host defines standard keys in the template (e.g., `close-btn`, `title-label`).
    * Module receives the Root GUID.
    * Module locates sub-elements using scoped queries:
        ```
        javascript
        const root = document.querySelector(`[data-GUID="${rootGuid}"]`);
        const closeBtn = root.querySelector('[data-ui-key="close-btn"]');
        ```
    * While "Atomic" components (like a single button) use a unique `data-GUID`, complex "Molecular" components (like a Rich Card with header, content, and action rows) use a relative addressing system to avoid backend overhead.

 2.  **Capacity (`data-mount-point`)**:
    * A named slot *inside* a component where *other* components can be injected.
    * **Default:** `data-mount-point="default"`
    * **Named:** Specialized slots (e.g., `data-mount-point="tab-headers"`).
    * *Leaf nodes (like Buttons or Inputs) do not have mount points.*

### 2.2 The Registration Contract (`UiRegistrationEntry`)
Modules do not write to the DOM. They register **Intent** in C#.
```
csharp
// Found in SoftwareCenter.Core.Frontend
public class UiRegistrationEntry
{
    public string OwnerModuleId { get; set; }
    public UiType Type { get; set; } // Layout, Widget, Script, Style
    public string ZoneId { get; set; } // Where to put it? (e.g., "zone-nav")
    public int Priority { get; set; }  // Higher overwrites Lower
    
    // The Content
    public string RawHtml { get; set; }
    public string RequiredCss { get; set; }
    public string RequiredJsController { get; set; }
}
```

---

## 3. The Composition Workflow
### Phase 1: Server-Side Composition (Initial Load)
When the user opens the application:
1. Host renders index.html (The Shell).
2. TagHelper <zone name="zone-nav" /> executes.
3. FrontendManager: 
   . Queries UiRegistry for all entries targeting "zone-nav".
   . Loads the Template (e.g., tpl-nav-item).
   . Hydrates the template (Replace {{Label}} with data, Generate data-GUID).
   . Returns the final HTML string.
4. Result: Browser receives a fully formed HTML page. Fast initial paint. SEO friendly (if needed).

### Phase 2: Client-Side Hydration (SignalR)
1. Browser loads host.js and connects to FrontendHub.
2. SignalR sends RegisterConnection(UserPrincipal).
3. Events: The page is now ready to receive live updates.

---

## 4. The Template System
*    **Modules should utilize the Standard Template Library to ensure visual consistency.
### 4.1 The "Rich Card" Pattern (Molecular Components)
A module wants a complex card. It shouldn't build it div-by-div.
Step 1: Module Request (C#)
```
C#
_frontend.Register(new UiRegistrationEntry {
    TemplateId = "tpl-card-rich",
    InitialData = new { Title = "Server Status" }
});
```
Step 2: FrontendManager Resolution
 - Loads standard-templates.html.
 - Extracts <template id="tpl-card-rich">.
```
HTML
<template id="tpl-card-rich">
    <div class="std-card std-card-rich" data-GUID="{{UI_COMPONENT_ID}}">

        <div class="std-card-rich-header" data-ui-key="header-row">
            <div class="std-card-rich-status" data-ui-key="status-indicator"></div>
            <div class="std-card-rich-title" data-ui-key="title-label">{{CONTENT}}</div>
            <button class="std-btn-icon-only" data-ui-key="close-btn">×</button>
        </div>

        <div class="std-card-rich-body custom-scrollbar" data-ui-key="content-body" data-mount-point="default">
            <div class="std-rich-text-group">
                <a class="std-rich-link" data-ui-key="link-primary">Primary Source Link</a>
                <div class="std-rich-subtext" data-ui-key="label-secondary">Secondary detail text goes here...</div>
            </div>
        </div>

        <div class="std-card-rich-actions" data-ui-key="action-row">
            <button class="std-btn-icon-only" data-ui-key="action-btn-1">✎</button>
            <button class="std-btn-icon-only" data-ui-key="action-btn-2">➜</button>
            <button class="std-btn-icon-only" data-ui-key="action-btn-3">🔗</button>
            <button class="std-btn-icon-only" data-ui-key="action-btn-4">🗑</button>

            <label class="std-toggle-switch" data-ui-key="action-toggle">
                <input type="checkbox" checked>
                <span class="std-slider-track"></span>
            </label>
        </div>

        <div class="std-card-rich-progress-container" data-ui-key="progress-row">
            <div class="std-progress-fill" style="width: 0%;" data-ui-key="progress-bar" data-initial-width="{{CONTENT}}"></div>
        </div>

    </div>
</template>
```
 - Hydrates {{CONTENT}} and generates new GUIDs for the buttons inside.
Step 3: Scoped JavaScriptThe module registers a specific JS controller to handle this card.
```
JavaScript
// Module: ServerStatusModule.js
window.ModuleRegistry["ServerStatus"] = {
    init: function(rootGuid) {
        // 1. Scoped Selection
        const root = document.querySelector(`[data-guid="${rootGuid}"]`);
        const actionBtn = root.querySelector(`[data-ui-key="btn-action"]`);
        
        // 2. Event Wiring
        actionBtn.addEventListener("click", () => {
            signalR.invoke("SendCommand", "RestartServerCommand", ...);
        });
    }
};
```

## 5. Standard Template Library
### 5.1 The Host provides these primitives via a 'standard-templates.html' file. Modules should rely on them.
```
Table: Basic Layout & Containers
Template||IDDescriptionMount||Points (Slots)
tpl-shell||The main grid layout||zone-nav, zone-header, zone-main
tpl-nav-item||A button in the sidebar||None
tpl-card-std||Standard white panel||default
tpl-grid-row||Flexbox row||default
tpl-input-group|Label + Input + Error||None
tpl-modal||Overlay dialog||header, body, footer
And lots more...
```

### 5.2 Styling GuideStyles are defined in 'style.css' and are globally applied. 
 - Modules should not inject custom CSS files unless absolutely necessary.
   CSS Variable Theme
```
CSS
:root {
    --primary-bg-color: #1a1a1d;    /* Main Background */
    --secondary-bg-color: #2a2a2e;  /* Panels / Cards */
    --tertiary-bg-color: #3a3a3e;   /* Headers / Hovers */
    --primary-text-color: #c5c6c7;  /* Main Text */
    --accent-color: #66fcf1;        /* Highlights / Active States */
    --accent-color-dark: #45a29e;   /* Button Backgrounds */
    --border-color: #4b4b4e;        /* Dividers */
}
```

 - Class Naming Convention:
    All standard template classes are prefixed with .std- to avoid collision with 3rd party libraries.
    For example,
    .std-button
    .std-card
    .std-input

### 5.3 UI Manifest Schema (JSON)
Modules communicate structure changes via a JSON Manifest.

### 5.3.1 The Envelope
| Property | Type | Description |
| :--- | :--- | :--- |
| `operation` | String | `inject`, `update`, `remove`. |
| `targetGuid` | UUID | The GUID of the parent container to modify. |
| `mountPoint` | String | The specific slot in the parent (default: "default"). |
| `rootComponent`| Object | The root of the tree to be injected. |

### 5.3.2 The Component Object
This recursive structure defines the UI tree.

| Property | Type | Description |
| :--- | :--- | :--- |
| `type` | String | **Standard:** Matches a Template ID (e.g., `tpl-button`).<br>**Custom:** Set to `"custom"` to provide raw HTML. |
| `content` | String | (Optional) Text content or value to inject into `{{CONTENT}}`. |
| `children` | Array | (Optional) List of Component Objects to nest inside this one. |
| `attributes` | Object | (Optional) Key-value pairs for HTML attributes (e.g., `{"src": "img.png"}`). |
| `rawHtml` | String | **Only if type="custom"**. The raw HTML string. Must include `data-GUID="{{UI_COMPONENT_ID}}"` to be manageable. |
| `rawCss` | String | **Only if type="custom"**. CSS string to be injected into the page head. |

```
JSON
{
  "type": "tpl-card-rich", 
  "content": "Optional direct content",
  "data": { 
     "Title": "New Title", 
     "Icon": "fa-check" 
  },
  "children": [] 
}
```

### 5.4 Custom Element Strategy
While the Host provides a standard library, Modules may need proprietary UI elements.

**The "Hybrid" Injection Logic:**
1.  **Standard Mode:**
    * Module requests `type: "tpl-card"`.
    * FrontendManager loads `tpl-card` from `standard-templates.html`.
2.  **Custom Mode:**
    * Module requests `type: "custom"`.
    * Module provides `rawHtml` string in the JSON.
    * FrontendManager takes the `rawHtml`, treats it exactly like a template, and injects the Runtime GUID.
    * *Note:* If `rawCss` is provided, FrontendManager checks if this CSS has already been added to the client. If not, it pushes a style update.

**Example Custom HTML payload:**
```
html
<div class="my-custom-widget" data-GUID="{{UI_COMPONENT_ID}}">
    <canvas id="my-chart"></canvas>
</div> 
```

### 5.5 Composite Template Strategy (The "Rich" Pattern)
This strategy allows Modules to request complex, multi-control widgets as a single unit, "hydrating" only the parts they need.

### 5.5..1. The Structure (Host Responsibility)
The Host provides a template with a fixed layout and standard `data-ui-key` attributes on interactive elements.

**Example: `tpl-card-rich` Structure**
1.  **Row 1 (Header):** Status Dot, Title, Close Button.
2.  **Row 2 (Body):** Scrollable area, Primary Link, Subtext.
3.  **Row 3 (Actions):** 4 Generic Icon Buttons, 1 Toggle Switch.
4.  **Row 4 (Footer):** Progress Bar.

### 5.5..2. The Logic (Module Responsibility)
Modules request the template by Type, then use Scoped JS to configure it.

**Step 1: The Manifest Request**
```
json
{
  "operation": "inject",
  "targetGuid": "...", 
  "mountPoint": "default",
  "rootComponent": {
    "type": "tpl-card-rich",
    "content": "Server Name (Default Title)" 
  }
}
```

**Step 2: The Scoped Controller Modules must perform "Cleanup & Wiring" in their Init function.
Select the Root by GUID.
Select sub-elements by Key.
Remove unused placeholders (e.g., if only 2 actions are needed, remove action-btn-3 and 4).
Configure text/icons/styles.
Wire events.
```
JavaScript
window.ModuleRegistry["MyModule"] = {
    init: function(cardGuid) {
        const root = document.querySelector(`[data-GUID="${cardGuid}"]`);

        // 1. Configure Title & Progress
        root.querySelector('[data-ui-key="title-label"]').textContent = "Main Database";
        root.querySelector('[data-ui-key="progress-bar"]').style.width = "45%";

        // 2. Wire "Close" Button
        root.querySelector('[data-ui-key="close-btn"]').addEventListener('click', () => {
             // Dispatch remove command...
        });

        // 3. Customize Action Buttons
        const editBtn = root.querySelector('[data-ui-key="action-btn-1"]');
        editBtn.innerHTML = "✎"; // Set Icon
        editBtn.addEventListener('click', handleEdit);

        // 4. Cleanup Unused Slots
        // (Example: We don't need buttons 3 and 4)
        root.querySelector('[data-ui-key="action-btn-3"]').remove();
        root.querySelector('[data-ui-key="action-btn-4"]').remove();
    }
};
```

---

## 6. The "Bridge": Backend to UI Communication
This is the critical link between GLOBAL_METADATA (Backend) and UI_METADATA (Frontend).
Scenario: A long-running task finishes in the Backend. The UI must update.
1. Backend (Module):
 - Work finishes.
 - Module publishes eventBus.Publish(new TaskCompletedEvent(taskId)).
 - Note: The Backend Module DOES NOT know about the UI.
2. Frontend (Infrastructure):
 - A generic NotificationHandler in SoftwareCenter.Frontend subscribes to TaskCompletedEvent.
 - It creates a UI Manifest (JSON) instructing the UI to change the spinner to a green checkmark.
 - It calls _hubContext.Clients.User(userId).SendAsync("UpdateUi", manifest).
3. Client (Browser):
 - host.js receives UpdateUi.
 - Finds the element by GUID.
 - Applies the change.

### 6.1 Interaction Flow Example
Scenario: User clicks "Check for Updates" on a Module's card.
1. User Action: Click event on DOM element (data-ui-key="btn-check").
2. Client JS: The Module's scoped JS intercepts the click.
3. Transport: JS calls hub.invoke("Dispatch", { Type: "CheckUpdatesCommand" }).
4. Backend Logic: * BackendManager receives Command.
 - Routes to UpdaterModule.Handle(CheckUpdatesCommand).
 - Logic runs (checks internet).
5. UI Update: UpdaterModule sends UpdateUiCommand targeting the status label GUID - a **UI Manifest** (JSON) typically via a command handler
 - Payload: `{ TargetGuid: "...", TemplateType: "card rich", Content: "..." }`
6. Templating: The FrontendManager looks up the `TemplateType` in `standard-templates.html`.
7. Hydration: The FrontendManager 
 - Extracts the HTML from the `<template>` tag. Example '<template id="tpl-card-rich">'.
 - Replaces `{{UI_COMPONENT_ID}}` with a new GUID.
 - Replaces `{{CONTENT}}` with the payload data.
 - FrontendManager pushes SignalR event: InjectHtml(targetGuid, "<span>Updated!</span>").
6. Rendering: Client DOM updates instantly without page reload
    * Finds the DOM element with `data-GUID` matching the Target.
    * Appends the new HTML into the Target's `data-mount-point`.

To support this, FrontendManager logic simply needs a small check during the "Template Lookup" phase.
```
csharp
// Inside FrontendManager.cs
public string ProcessComponent(ComponentJson component)
{
    string templateHtml = "";

    // 1. DETERMINE SOURCE
    if (component.Type == "custom")
    {
        // Source A: The Module provided it directly
        templateHtml = component.RawHtml;
        
        // Handle CSS if present
        if (!string.IsNullOrEmpty(component.RawCss))
        {
            _signalRHub.SendStyleInjection(component.RawCss);
        }
    }
    else
    {
        // Source B: Look up in Host Library
        templateHtml = _templateLibrary.GetTemplate(component.Type);
    }

    // 2. GENERATE IDENTITY
    string newGuid = Guid.NewGuid().ToString();

    // 3. HYDRATE
    // This logic is identical for both Standard and Custom types!
    string processedHtml = templateHtml
        .Replace("{{UI_COMPONENT_ID}}", newGuid)
        .Replace("{{CONTENT}}", component.Content);

    // 4. RECURSION (Process Children)
    if (component.Children != null)
    {
        foreach(var child in component.Children)
        {
            // Find the mount point in the processed HTML and insert child
            // (In reality, you'd likely append children to the output string 
            // and let the client JS handle the DOM nesting, or use an HTML parser here)
        }
    }

    return processedHtml;
}
```

---

## 7. Folder Structure & Files
### 7.1 Frontend Asset Structure
```
Plaintext
/wwwroot
  /css
    /theme.css       (Global variables)
    /components.css  (Standard classes)
  /js
    /host.js         (SignalR connection, Event delegation)
    /registry.js     (Client-side module registry)
  /templates
    /standard.html   (The <template> definitions)
```

### 7.2 Core Classes (C#)
 - FrontendManager Project:
   * UiCompositionHandler: Handles the logic of merging templates.
   * TemplateEngine: Replaces {{Tokens}} with data.
   * FrontendHub: The SignalR connection point.