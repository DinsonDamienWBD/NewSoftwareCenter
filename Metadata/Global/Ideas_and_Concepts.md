Ideas and Concepts Registry

1. OPERATIONAL PROCEDURES & CONTEXT MANAGEMENT

Title: Multi-Chat Context Setup

Project: All Projects, Project_Overview_Compressed
Status: Not Implemented
Details: Action: Start new chat per project. Instructions: 1. Load ALL files from target project folder (/Host/, /Kernel/, etc.). 2. Load ONLY Global Metadata files from /Global Metadata/. 3. Load SPECIFIC sections from this Ideas_and_Concepts.md document. 4. STRICTLY IGNORE all other project code.

Title: Context Reset/Continuation

Project: All Projects
Status: Not Implemented
Details: To resume work, start a fresh chat following the Multi-Chat Context Setup, ensuring specific prompts are provided (prompts are stored as backups in My Drive > Project > Prompts.txt). Also, each project contains its own metadata under the project’s root folder > Project Metadata. As a rule, this metadata should be kept up to date. Thus, in a new chat, providing the global metadata as well as the project specific metadata should be enough to provide the correct and up to date context (without having the over-burden of understanding the whole project).

Title: Code vs. Metadata Update Policy

Project: All Projects
Status: Not Implemented
Details: a. Always update own metadata and history files under project’s root folder > Project Metadata Ideas_and_Concepts.md before any code implementation, update, or bug fix. 

2. CORE TECHNICAL CONCEPTS (Kernel & Host)

Title: Smart Router Execution Flow

Project: Kernel
Status: Not Implemented
Details: HOST requests Command -> Kernel (Router) prioritizes Handlers (Module > Host) -> Executes highest priority. If fail/unavailable, check FallbackDirective. If OK, execute next handler. Append Responder ID to response.

Title: UI Styling Service Enforcement

Project: Host, Kernel
Status: Not Implemented
Details: I U I D E F in Kernel defines abstract content. HOST handles the overall layout, theme, and placement using templates (Rule 16).

Title: Dynamic Module Loading

Project: Host, Kernel
Status: Not Implemented
Details: Use dynamic assembly loading (e.g., AssemblyLoadContext in .NET Core) for isolation and unloading capability. Modules are loaded from HOST/bin/net8.0/Modules/ModuleId/.

Title: Developer Feedback / Registry

Project: Kernel
Status: Not Implemented
Details: Kernel provides runtime-discoverable API: GetRegisteredCommands, GetRegisteredRoutes, GetCommandMetadata (includes owner, version, status like deprecated).

Title: Module Full View (Pop-Out SPA)

Project: Kernel, Host, All Modules
Status: Not Implemented
Details: New Feature: Modules can register a dedicated SPA URL (/module/ID/full). HOST automatically provides a "Pop Out" button/icon on the embedded UI fragment. SPA uses the Global Data Bus for synchronization upon closure.

3. FEATURE IMPLEMENTATION IDEAS (Modules)

Title: Advanced Credential Retrieval

Project: MDL.C R D M N G
Status: Not Implemented
Details: Module should utilize Windows Credential Manager (WCM) as well as a database-backed roaming profile for credential storage, overriding the HOST's basic encrypted text file handler. The credential retrieval will always be a WCM-first, fallback to DB approach. Credential storage will always be a WCM-cache + DB approach. For a new machine, as long as the DB is connected, the other credentials can be downloaded and cached into that machine’s WCM.

Title: Source Manifest Integration

Project: MDL.S R C M N G
Status: Not Implemented
Details: Ability for SourceManager to pull application manifests from Microsoft cloud services (SharePoint, OneDrive) using Graph/MSAL dependencies discovered in the original project analysis.

Title: Complex Installation

Project: MDL.I N S T L L R
Status: Not Implemented
Details: Installer module enhances the HOST's basic executable handler by supporting silent installs, argument passing, prerequisite checks, and handling Windows auto-restart requests. (Launcher - Host combination can act together to create a shortcut to launcher to the Common/User Startup folder, so that when Windows restarts, launcher is started, which in turn can start Host automatically. Once Host is started up, remove the launcher shortcut from startup folder.

Title: Diagnostic Logging Sinks

Project: MDL.L G G N O T
Status: Not Implemented
Details: LoggerNotif module implements Serilog to override HOST logging, supporting sinks to file, remote endpoint (e.g., Application Insights), and a UI Notification Center.

Title: Admin DB Management UI

Project: MDL.D B M N G R
Status: Not Implemented
Details: New module provides an ADMIN-level UI for direct C R U D operations on application database records (not available in HOST).

4. LAUNCHER & INSTALLER SERVICE NOTES

Title: Launcher Process Control

Project: L A N C H R, H O S T
Status: Not Implemented
Details: Launcher provides on-demand execution of HOST: Run HOST as Local User and Run HOST as Admin. Watchdog functionality: Launcher stops if HOST is manually stopped, but survives HOST restarts.

Title: Installer Service Protocol

Project: I S V C, MDL.I N S T L L R
Status: Not Implemented
Details: Service runs continuously as ADMIN. Listens on a secure local port or message queue for 'Push Installation/Update' commands from the HOST (via the Kernel) across multiple, configurable Channels. Machines having InstallerService running can subscribe to specific channels, and when installations are pushed to the specific channel, those machines are able to run these installations in a silent, no-user-intervention-needed way. InstallerService also supports a direct-to-a-specific-machine push, which overrides a channel push so as to provide remote installs on specific machines only, instead of a channel wide install.

5. DATA STRUCTURES & ALGORITHMS (Manifests & Logic)

Title: Standardized App Manifest (JSON)

Project: MDL.A P P M N G, MDL.S R C M N G
Status: Not Implemented
Details: Define a strict JSON schema for applications. Fields: Id, Name, Version, InstallCommand, UninstallCommand, DetectionMethod (File/Registry), Dependencies (List of AppIds).

Title: Recursive Dependency Resolution

Project: MDL.A P P M N G
Status: Not Implemented
Details: Implement a recursive resolver. Before installing App A, check Dependencies list. For each dependency, check its dependencies. Install strictly from Bottom-Up (Leaves first, Root last). Detect and error on circular references.

Title: Unified Inventory Merging

Project: MDL.A P P M N G
Status: Not Implemented
Details: Logic to merge manifests from multiple SourceProviders. Rules: 1. Same ID + Same Version = Deduplicate. 2. Same ID + Higher Version = Update Available. 3. Source Priority (Local > Remote).

Title: Installation State Machine

Project: I S V C, MDL.A P P M N G
Status: Not Implemented
Details: Robust state tracking: Pending -> Downloading (with %) -> HashCheck -> Installing Dependencies -> Installing Main -> Verifying -> Success/Fail. Support Rollback on failure.

Title: Topological Sort (Kahn's Algorithm)

Project: MDL.A P P M N G
Status: Not Implemented
Details: Use Kahn's Algorithm for runtime calculation of Install Order from a Dependency Graph. Detect circular dependencies if the sorted list count < total items.

Title: Directory Completeness Check (DCC)

Project: MDL.S R C M N G
Status: Not Implemented
Details: Check if App is "Ready to Install" by comparing Remote Scope vs Local Scope. Logic: Iterate DB Apps -> Get Remote File List (Size/Name) -> Get Local File List -> Compare. Status: Ready, Incomplete, NotDownloaded.

Title: Manifest Integrity Check

Project: MDL.S R C M N G
Status: Not Implemented
Details: Enhancement to DCC. Instead of simple file scan, read manifest.json in Scope root. Validate presence of requiredFiles and verify checksums (SHA256).

6. UI COMPONENTS & VISUALIZATION

Title: Custom Tree View Control

Project: MDL.S R C M N G, H O S T
Status: Not Implemented
Details: A recursive, collapsible Tree View for browsing Remote Repositories/Folders. Must use Rule 25 (Design Tokens) to match Host theme. Implemented as CustomHtmlContent injected into a Card.

Title: Dependency Graph Visualizer

Project: MDL.A P P M N G
Status: Not Implemented
Details: A read-only visualizer (likely using the Custom Tree View) to show the user the full tree of what will be installed before they click "Go". E.g., User selects "Game", Tree shows: "Game -> DirectX -> VC++ Runtime".

Title: Reactive Progress Indicators

Project: MDL.A P P M N G
Status: Not Implemented
Details: Dynamic UI swapping. Replace "Install" button with "Progress Bar" using BindKey updates. Ensure progress state persists in Host Shadow State even when user navigates away.

Title: Hybrid Tree + Hash Map UI Data

Project: MDL.S R C M N G (UI)
Status: Not Implemented
Details: Use a Node class with Children (Tree) AND a flattened Dictionary<Id, Node> (Hash Map). Allows O(1) lookups for selection updates while maintaining hierarchy for rendering.

7. OFFLINE & SYNC STRATEGY

Title: Cache & Submit Model

Project: MDL.D B M N G, MDL.S R C M N G
Status: Not Implemented
Details: Separation of Read/Write. Read from inventory-metadata.json (Cache). Write new items to upload_queue.json. Background Sync Service processes queue when online.

Title: Offline Manifest Export

Project: MDL.D B M N G
Status: Not Implemented
Details: Function ExportDatabaseToJson() runs on schedule/change. Saves DB state to local JSON. Host loads this JSON if DB connection fails (Offline Mode).

8. Distributed Tracing & Telemetry

Title: Context Propagation and Interceptor/Proxy patterns.

Project: Kernel
Status: Not Implemented
Details: Kernel will enforce it using these contracts.
The "Proxy" Concept: 
Kernel loads Module A.
Kernel creates a "ModuleContext" for A.
    var proxyRouter = new RouterProxy(realRouter, "ModuleA");
Kernel calls moduleA.InitializeAsync(proxyRouter);
Module A sends a command:
    Code: await _router.RouteAsync(new Command("Save"));
Note: Module A did not set SourceId or TraceId.
Proxy Intercepts:
    RouterProxy.RouteAsync executes:
    command.TraceId = TraceContext.CurrentTraceId ?? Guid.NewGuid();
    command.History.Add(new TraceHop("ModuleA", "Sent"));
Passes to Real Router.
This guarantees 100% enforcement. The module literally cannot send a message without the Proxy stamping it first.