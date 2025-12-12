# UI Architecture & Composition Framework
**Project:** SoftwareCenter.Host
**Version:** 2.0 (Refactored)

---

## 1. Overview
This project utilizes a **Composite UI (Micro-Frontend)** architecture. The "Host" application provides the shell, layout, and styling, while various "Modules" provide functional UI fragments.

The composition logic is handled by a backend **UIManager**, which orchestrates the injection of HTML fragments into specific slots (Zones) within the Host, ensuring a unified look and feel via a centralized Template Library.

---

## 2. Core Concepts

### 2.1. Identity vs. Capacity
Every UI component (Zone or Fragment) is defined by two primary attributes:

1.  **Identity (`data-GUID`)**:
    * A unique identifier assigned at runtime by the UIManager.
    * Allows the component to be targeted for updates, removal, or interaction.
    * **Format:** UUID string (e.g., `123e4567-e89b-12d3-a456-426614174000`).

2.  **Capacity (`data-mount-point`)**:
    * A named slot *inside* a component where *other* components can be injected.
    * **Default:** `data-mount-point="default"`
    * **Named:** Specialized slots (e.g., `data-mount-point="tab-headers"`).
    * *Leaf nodes (like Buttons or Inputs) do not have mount points.*

### 2.2. The Injection Tokens
The `index.html` file does not contain hardcoded IDs for zones. Instead, it uses **Injection Tokens** that the UIManager parses and replaces.

* **Format:** ``
* **Example:** ``

---

## 3. Workflow & Composition

### 3.1. Server-Side Composition (The "Initial Load")
1.  **Host Start:** UIManager reads `index.html`.
2.  **Discovery:** UIManager finds all `` tokens.
3.  **Processing:**
    * Loads the corresponding `[name]-zone.html`.
    * Injects a Runtime GUID into `{{UI_COMPONENT_ID}}`.
    * Replaces the token in `index.html` with the processed HTML.
4.  **Serve:** The fully composed HTML is sent to the browser.

### 3.2. Dynamic Injection (Runtime / SignalR)
1.  **Request:** A Module sends a **UI Manifest** (JSON) to UIManager.
    * *Payload:* `{ TargetGuid: "...", TemplateType: "card", Content: "..." }`
2.  **Templating:** UIManager looks up the `TemplateType` in `standard-templates.html`.
3.  **Hydration:**
    * Extracts the HTML from the `<template>` tag.
    * Replaces `{{UI_COMPONENT_ID}}` with a new GUID.
    * Replaces `{{CONTENT}}` with the payload data.
4.  **Transport:** UIManager sends the hydrated HTML to the client via SignalR.
5.  **Rendering:**
    * Client `app.js` receives `InjectFragment` event.
    * Finds the DOM element with `data-GUID` matching the Target.
    * Appends the new HTML into the Target's `data-mount-point`.

---

## 4. File Structure Standards

### 4.1. Zone Files
Zones must be wrapped in a container that defines their identity.
```html
<div class="zone-wrapper" data-GUID="{{UI_COMPONENT_ID}}">
    <div id="app-title">Software Center</div>
    
    <div id="titlebar-controls" data-mount-point="default"></div>
</div>

### 4.2. Template Library (standard-templates.html)A single file containing all atomic UI building blocks.Format: Standard HTML <template> tags.ID Convention: tpl-[component-name] (e.g., tpl-button, tpl-card).

---

## 5. Interaction Model (Scoped JS)Modules cannot access the global DOM directly. They must use Scoped Controllers.Registration: Modules provide a raw JS file alongside their UI request.Initialization: The Host calls the module's Init(guid) function.Scoping: The module uses the guid to query only its own DOM subtree.JavaScript// Standard Pattern for Module Logic
window.ModuleRegistry["MyModule"] = {
    init: function(uniqueGuid) {
        // SCOPED SELECTION
        const root = document.querySelector(`[data-GUID="${uniqueGuid}"]`);
        const btn = root.querySelector('.std-button');
        
        // INTERACTION
        btn.addEventListener('click', () => {
             // LOGIC...
        });
    }
};

---

## 6. Template Reference LibraryContainers & LayoutTemplate IDDescriptionMount Pointstpl-divGeneric generic wrapperdefaulttpl-sectionSemantic sectiondefaulttpl-panelStyled card/paneldefaulttpl-grid12-column Grid containerdefaulttpl-rowFlex row for griddefaulttpl-col-XColumns (sizes: 12, 6, 4, 3, auto)defaulttpl-detailsExpandable accordion itemdefaultForm InputsTemplate IDDescriptionNotestpl-input-textStandard text boxtpl-input-numberNumeric inputtpl-input-dateDate pickertpl-checkbox2-state CheckboxWrapped in labeltpl-toggleSwitch-style toggleCheckbox hacktpl-selectDropdownOptions injected via JStpl-input-groupInput + Button wrapperMerged bordersInteractive ElementsTemplate IDDescriptionNotestpl-buttonStandard buttontpl-button-groupSegmented buttonsMerged borderstpl-tree-folderExplorer tree parentUses <details>tpl-tree-itemExplorer tree leaftpl-tabsTab containerHas tab-headers & tab-panelstpl-dialogModal dialogNative <dialog>Feedback & VisualsTemplate IDDescriptionNotestpl-progressProgress bartpl-spinnerLoading spinnerCSS animationtpl-toastNotification popupFixed positiontpl-badgeSmall status pilltpl-skeleton-textLoading placeholderPulse animation

---

## 7. Styling GuideStyles are defined in wwwroot/css/style.css and are globally applied. Modules should not inject custom CSS files unless absolutely necessary.CSS Variable ThemeCSS:root {
    --primary-bg-color: #1a1a1d;    /* Main Background */
    --secondary-bg-color: #2a2a2e;  /* Panels / Cards */
    --tertiary-bg-color: #3a3a3e;   /* Headers / Hovers */
    --primary-text-color: #c5c6c7;  /* Main Text */
    --accent-color: #66fcf1;        /* Highlights / Active States */
    --accent-color-dark: #45a29e;   /* Button Backgrounds */
    --border-color: #4b4b4e;        /* Dividers */
}
Class Naming ConventionAll standard template classes are prefixed with .std- to avoid collision with 3rd party libraries..std-button.std-card.std-input

---

## 8. UI Manifest Schema (JSON)
Modules communicate structure changes via a JSON Manifest.

### 8.1. The Envelope
| Property | Type | Description |
| :--- | :--- | :--- |
| `operation` | String | `inject`, `update`, or `remove`. |
| `targetGuid` | UUID | The GUID of the parent container to modify. |
| `mountPoint` | String | The specific slot in the parent (default: "default"). |
| `rootComponent`| Object | The root of the tree to be injected. |

### 8.2. The Component Object
This recursive structure defines the UI tree.

| Property | Type | Description |
| :--- | :--- | :--- |
| `type` | String | **Standard:** Matches a Template ID (e.g., `tpl-button`).<br>**Custom:** Set to `"custom"` to provide raw HTML. |
| `content` | String | (Optional) Text content or value to inject into `{{CONTENT}}`. |
| `children` | Array | (Optional) List of Component Objects to nest inside this one. |
| `attributes` | Object | (Optional) Key-value pairs for HTML attributes (e.g., `{"src": "img.png"}`). |
| `rawHtml` | String | **Only if type="custom"**. The raw HTML string. Must include `data-GUID="{{UI_COMPONENT_ID}}"` to be manageable. |
| `rawCss` | String | **Only if type="custom"**. CSS string to be injected into the page head. |

---

## 9. Custom Element Strategy
While the Host provides a standard library, Modules may need proprietary UI elements.

**The "Hybrid" Injection Logic:**
1.  **Standard Mode:**
    * Module requests `type: "tpl-card"`.
    * UIManager loads `tpl-card` from `standard-templates.html`.
2.  **Custom Mode:**
    * Module requests `type: "custom"`.
    * Module provides `rawHtml` string in the JSON.
    * UIManager takes the `rawHtml`, treats it exactly like a template, and injects the Runtime GUID.
    * *Note:* If `rawCss` is provided, UIManager checks if this CSS has already been added to the client. If not, it pushes a style update.

**Example Custom HTML payload:**
```html
<div class="my-custom-widget" data-GUID="{{UI_COMPONENT_ID}}">
    <canvas id="my-chart"></canvas>
</div>  

### 3. Workflow Logic for UIManager (Pseudocode)

To support this, your UIManager logic simply needs a small check during the "Template Lookup" phase.

```csharp
// Inside UIManager.cs

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