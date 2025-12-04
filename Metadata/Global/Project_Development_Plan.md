Global Project Development Plan (Phased)
1.	S Y S T E M F L O W O V E R V I E W
1.1.	Architectural Patterns:
1.1.1.	Microkernel: The HOST loads only the KERNEL. The KERNEL is responsible for loading all other MODULES.
1.1.2.	BFF (Backend for Frontend): The HOST acts as the BFF.
1.1.3.	Pure Command Bus: The KERNEL routes requests based purely on Command Name Strings.
1.1.4.	Template Engine (UI): The HOST loads raw HTML templates to render Abstract Content.
1.1.5.	Shadow State (Persistence): The HOST maintains an in-memory "Shadow Copy" of all UI values (BindKey -> Value) to support tab switching without losing progress.
1.2.	Startup Sequence (Microkernel):
1.2.1.	Host Start: Initialize BasicRouter (Local File/Creds only).
1.2.2.	Kernel Check: Scan for SoftwareCenter.Kernel.dll.
1.2.3.	Branch:
•	Missing: Continue in Safe Mode (No Modules loaded).
•	Present: Load Kernel -> Kernel.Initialize() -> Replace Router.
1.2.4.	Kernel Initialization: Kernel scans Modules/ folder -> Loads DLLs -> Calls IModule.Register.
1.3.	UI Composition Flow (Reactive & Persistent):
1.3.1.	Zone Layout: HOST MUST provide 5 Mandatory Zones.
1.3.2.	Card Definition: Modules send ContentPart with ViewId (GUID) and Control Arrays.
1.3.3.	State Tracking: Host updates Shadow Dictionary on UIUpdate events.
1.3.4.	Reactivity: Controls use BindKey for 1:1 updates (e.g., "SQL_Progress").
2.	C O N T R A C T S P L A N (The Shared Language)
Priority: Critical | Dependency: None | New Project: SoftwareCenter.Contracts
2.1.	Phase 1: Definitions
2.1.1.	ICommand, IResult (Generic Dictionary), IModule.
2.1.2.	IRouter (Implemented by both Host and Kernel).
2.1.3.	UI Models (ContentPart, UIZone, UIControl).
2.1.4.	LogEntry and LogLevel enums.
3.	K E R N E L P L A N (The Intelligence Plugin)
Priority: Critical | Dependency: Contracts
3.1.	Phase 1: Implementation
3.1.1.	Implement SmartRouter : IRouter.
3.1.2.	Implement HandlerRegistry (The Bus).
3.1.3.	Module Loader: Implement AssemblyLoadContext logic to find/load Modules.
3.1.4.	Exception Barrier: Wrap all module calls in try/catch blocks.
4.	H O S T P L A N (The Standalone Base)
Priority: High | Dependency: Contracts
4.1.	Phase 1: Functionality (Safe Mode)
4.1.1.	Implement BasicRouter : IRouter (Handles "File.List" locally).
4.1.2.	Implement KernelLoader (Reflection logic to find/start Kernel).
4.1.3.	Implement DefaultHandlers (Async).
4.2.	Phase 2: UI Shell & Style Engine
4.2.1.	Style Registry: Service for CSS Variables.
4.2.2.	Templates: Raw HTML template loading.
4.2.3.	Renderer: Logic to iterate controls and inject HTML.
4.2.4.	Shadow State: Service for UI persistence.
5.	M O D U L E P L A N S (The Advanced Features)
Priority: Medium | Dependency: Contracts
5.1.	AppManager:
5.1.1.	Logic: Long-running Install task, recursive dependency resolution (DAG).
5.1.2.	UI: Search/Results Cards.
5.2.	SourceManager:
5.2.1.	Logic: File I/O commands ("Source.List", "Source.Copy"). Directory Completeness Check.
5.2.2.	UI: Custom Tree View (via HTML Injection).
5.3.	CredManager:
5.3.1.	Logic: Secure Storage (WCM/DB).
5.4.	DBManager:
5.4.1.	Logic: Persistence commands ("History.Add", "Cache.Get"), Offline Manifest Export.
5.5.	LoggerNotif:
5.5.1.	Logic: Matrix Filter (Level vs Verbosity).
5.6.	AI.Agent (Copilot Integration):
5.6.1.	Logic:
5.6.2.	Integrate Microsoft Copilot / Azure OpenAI API.
5.6.3.	Implement "Command Translator" (Natural Language -> ICommand).
5.6.4.	UI: Chat Zone.
6.	L A U N C H E R & S E R V I C E P L A N
Priority: Low (Day 2) | Dependency: Contracts
6.1.	Phase 1: Launcher (User Context)
6.1.1.	Watchdog: Check if HOST PID is alive.
6.2.	Phase 2: Installer Service (Admin Context)
6.2.1.	Implement Worker Logic.
6.2.2.	Implement Listener: Pub/Sub Channels for "Push Install".
7.	B E S T O R D E R O F I M P L E M E N T A T I O N
7.1.	CONTRACTS (Phase 1): Define ICommand, IModule, IRouter. (The absolute foundation)
7.2.	KERNEL (Phase 1): Implement SmartRouter and ModuleLoader. (The Brain)
7.3.	HOST (Phase 1): Setup ASP.NET, BasicRouter, and Kernel integration. (The Body)
7.4.	HOST (Phase 2): Implement Shadow State and Template Renderer. (The UI)
7.5.	MODULES (LoggerNotif): Implement basic logging. (The Voice)
7.6.	MODULES (SourceManager): Implement File I/O. (The Hands)
7.7.	MODULES (AppManager): Implement Logic. (The Skills)
7.8.	LAUNCHER & SERVICE: Implement system processes.
7.9.	AI AGENT: Connect Copilot.
8.	P L N S & D O C S (References)
D O C N A M E	C O N T E N T
Project_Overview_Compressed.md	Rules, Project Map, Compressed Architecture.
Ideas_and_Concepts.md	Feature ideas, Transient tech notes.
Project_Development_Plan.md	Global Phased Development Plan & Data Flow.
Project_History_Log.md	(Inside each project folder) Local changes log.

## Technical Decisions & Standards

### 1. The "Smart Router" Protocol
* **Routing Logic:** The Kernel's `SmartRouter` is authoritative. It does not fallback; it imports Host capabilities as standard handlers during initialization.
* **Exception Barriers:** (Rule 17) The Kernel wraps every execution in a `try/catch` block to prevent Module crashes from bringing down the Host.

### 2. Global Data Store
* **Technology:** **LiteDB** (Serverless NoSQL).
* **Access:** Managed by Kernel, exposed via `IGlobalDataStore`.
* **Usage:** Allows Host and Modules to store/search complex nested objects (dictionaries, lists, blobs) without schema migrations.

### 3. Developer Experience (DX)
* **Rich Metadata:** All commands must register with `RouteDefinition` containing:
    * `Description`: Human-readable help text.
    * `Status`: `Active`, `Deprecated` (warns), or `Obsolete` (blocks).
* **Discovery:** The Kernel provides runtime discovery of all loaded routes and modules for UI generation.

### 4. Module Isolation
* **Technology:** `System.Runtime.Loader.AssemblyLoadContext`.
* **Strategy:** Each module loads in its own isolated context to prevent version conflicts (Dependency Hell) and allow Hot-Swapping (Unloading).