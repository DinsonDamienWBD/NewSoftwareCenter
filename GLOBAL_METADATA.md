# Software Center - Global Architecture Metadata

## Overview

This document outlines the high-level architecture of the Software Center application. The architecture is designed as a decoupled, extensible, module-first platform. It is built around a central **Kernel** that acts as a service broker and communication bus. The **Host** application provides the entry point and default functionalities, which can be extended or replaced by **Modules**.

## Core Principles

1.  **Decoupling:** Components (Host, Modules) should not reference each other directly. All communication flows through the Kernel via commands, events, or API calls.
2.  **Dynamic Discovery:** The Kernel discovers services, commands, and APIs at runtime. Modules register their capabilities, and other components can query the Kernel to discover and use them without compile-time dependencies.
3.  **Host as Default Provider:** The Host application is a fully functional, standalone application that provides basic implementations for all core features (e.g., logging, installation).
4.  **Modules as Specialists:** Modules provide advanced or alternative implementations of features. They can replace the Host's default providers.
5.  **Single UI Authority:** The `UIManager` is the only component that can manipulate the user interface. All UI changes are requested via commands sent to the `UIManager`.
6.  **Minimal Core Contract:** The `Core` library contains only the essential, stable interfaces and models required to participate in the ecosystem (`IModule`, `ICommand`, etc.). It is the only project Modules are required to reference.

## Project Dependencies

The fundamental dependency graph is as follows:

```
              +-----------------+
              |      Host       |
              +--------+--------+
                       |
           +-----------+-----------+
           |                       |
           v                       v
+-----------+-----------+ +---------+-----------+
|         Kernel        | |       UIManager     |
+-----------+-----------+ +---------+-----------+
           |                       |
           +-----------+-----------+
                       |
                       v
+-----------------------+-----------------------+
|                      Core                     |
+-----------------------------------------------+
           ^
           |
+-----------+-----------+
|        Modules        |
+-----------------------+
```

A. Project Struture/Architecture policy:
1. Minimum dependencies on each other.
2. Plugin style
3. Host - the main app, the body
a. runs a Kestrel webserver.
b. provides UI*
c. provides base services*
* - more details later down

4. Core - a minimal contract. Modules must implement IModule
5. Kernel - the brain
a. Command bus, event bus, BFF
b. Provides smart routing**
c. Provides Service registry**
d. Provides Global Data Store**
e. Provides dynamic Module loading/unloading
f. Provides 'getRegistryManifest' or some such at runtime to provide runtime developer documentation for module developers
** - More details later down.

6. UIManager - The frontend/UI controller
a. All UI render/updates go through here.
b. Tags each and every UI element with a unique ID
c. Host & modules request for UI element create/add/edit/delete/load/unload etc. to UIManager***
*** - more details later down

B: Dependencies (desired)
1. As much decoupled as possible.
2. Use API calls, federated calls to reduce direct reference
3. Host can directly refer Kernel & UIManager (and Core if necessary)
4. Kernel & UIManager directly refer Core, but should never refer each other.
5. Modules should only refer Core to implement IModule
6. Module developers will just be provided the exe, dll and documentation files.
They shouldn't cause/nesseciate changes to Host, Kernel, UIManager & Core code


C. Desired flow:
1. Host runs.
a. Starts Kestrel Webserver
b. Loads Kernel
c. Loads UIManager
(Core can be loaded if necessary)
d. Registers it's basic services/capabilities and the API endpoints & Commands to use these services with Kernel's Service Registry with a low priority - *As mentioned above
e. Registers it's Ui framework and UI fragments with UIManager
f. Kernel maintains the service registry, requiring whoever is adding an endpoint/command to also provide rich metadata for using these services (the XmlDocumentCollectionExtension comes into play here so that it can gather and make use of the ///Summary comments to auto-generate the rich metadata. Either the developer must explicitly code for providing this metadata when registering services or properly document the code - any one of these ways will work. The rich metadata is mandatory)
g. Kernel scans a partially hardcoded directory (Host exe folder/Modules/ModuleProjectName folders) for dlls which implement IModule and loads and initializes them.
h. During this phase, the loaded modules will also register their own services, capabilities and commands with a higher priority (for example, Host registers a 'EditSource' command/service with low priority. A future 'SourceManager' module can register the same 'EditSource' command/service with a higher priority, the difference being that Host's edit source will only allow you to edit the path to a local folder, while the SourceManager module provides advanced functionality supporting not only local, but remote folders, webserver locations etc. In this case, the higher priority command/service is the one that gets called whenever anyone Host or module calls this endpoint/command. - ** As mentioned above
i. kernel routes all requests based on this priority using the information in its service registry; and provides a data store which anyone Host or modules can use o store and share data. the data is fully traceable and accountable with audit history. - ** As mentioned above
j. Kernel attaches TRACE to each and every request during the whole request-response pipeline for the whole lifetime of the pipeline (this trace information which includes the whole history of who requested what, who routed it where, who serviced the request, how the response was routed back, the entire life history of that request-response with each and every hop, can be used for some advanced loging if necessary)
k. Kernel also provides scheduling background jobs
l: Kernel also supports dynamic loading/unloading of modules, so the service registry and routing is completely dynamic.

D. UI Flow - *** As mentioned above
1. Host provides the basic framework of the SPA (the zones - title bar, notification bell icon and flyout, nav area, contents area etc., and an index.html/css/js that ties them together into an SPA)
2. Host also provides the template for the smaller group containers (currently a card style interface) which displays related information and is actionable in that it can include not only labels but also interactive Ui elements like input, buttons, toggles etc.
3. Host can also provide templates for generic UI controls like textboxes, dropdowns, toggles and stuff.
* The full UI is a COMPOSITE UI with both Host & Modules contributing in as below:
4. Initially Host passes its zone definitions and index html/css/js files to UIManager which composes them into a proper SPA and serves them to the kestrel server to be displayed in the browser tab.
5. UIManager assigns a unique tag/ID to each element and registers who owns each piece of the UI - during this time, as our app is starting up, it should be Host supplying the basics. The modules are still being loaded. And returns the IDs back to the requestor - host
6. Next, Host requests UIManager to create 1 or more Nav buttons & related main content containers.
7. UIManager does this (the style & theme inherit from the main SPA which UIManager has already composed in the previous steps), again assigns IDs to each of them, registers Host as the owner, returns the IDs to Host, and injects these elements into the SPA, so the nav buttons appear in the nav area, and are linked to the respective containers which would be displayed in the content area on clicking the specific nav buttons.
8. Next, for each Nav button - content container pair, Host can request one or more smaller group containers - the card interface (again, Host itself provides the actual template and style). 
9. UIManager generates the cards, attached IDs, injects the cards into the specified main content container, and returns the IDs to Host, which can now keep track of which card belongs to which content container using its own table of IDs of all the UI element it owns.
10. next, for each such card, Host can request UIManager to generate UI controls (text, labels, buttons, links, toggles etc. - again, host provides the templates).
11. UIManager generates the controls in a left-to-right, top-to-bottom way in the exact order as equested by Host, injects them into the specified card, and returns the IDs of each control back to Host.
12. During this time, Kernel is dynamically loading modules in the background.
13. Each module can request UIManager for its own 'nav button - content container' or override one from the Host. Again, priority is used here (Host registers a Source management nav button and container. A future SourceManager module can register a Source management nav button - content container with a higher priority, and UIManager composes the one with the higher priority. So If SourceManager module is not loaded, the user will see Host's source management nav button, content container and the UI inside it, but if the source manager module is loaded, the user will only see the SourceManager module's nav button, content container and UI controls, but can still see a different 'app Manager' Host UI unless a AppManager module has overridden that also)
14. Module's UI works in exactly the same 'request UIManager' for xyz, UIManager generates them using the template provided by Host, attach IDs, inject them into the proper places in the final UI, and return the IDs back to the requestor.
15. Host & Modules can request UIManager to Edit/Delete any UI element (nav button, content container, cards, UI controls inside the cards or anything) using the ID of that element. Provided the requestor is either the owner of that UI element, or shares ownership - in which case, it depends on its permission levels.
16. As mentioned each module or Host is the default owner of the pieces of UI it requested, and can choose to share the ownership, or even completly hand over the ownership if and when needed.
17. It is the reqponsibility of the owner to handle all interactions and animations and data inside the UI element they own. All clicks, hovers, animations should be handled by the UI owner. They send the necessary command/request to UIManager who can update the final UI accordingly.

E. Important considerations
1. Everything should be fully non-blocking
2. Host provides basic services, usually limited to the local machine on which it is running, or simple text file based approaches, or using Windows default actions for double clicks etc.
3. Modules can override Host's services to provide advanced services like remote & webserver access, double clicks handled with additional 'switches 6 arguments' and others.
4. Modules can provide COMPLETELY NEW functionalities also, by registering them with the service registry.
5. Along with the UI Fragments that modules request UIManager to generate, modules can provide fully custom html/css/js code that UIManager can inject into the proper place in the final UI as requested by the modules.
6. Modules can also optionally register 'FULL SPA(s)' of their own. In this case, when UIManager generates a nav button and Content container for that module, it will automatically add a pop-out button for each full SPA registered by that module.
7. For such module SPA, it is completely owned by the module and is fully handled by the module itself. It is the module's responsibility to reflect the proper changes which a user makes in it's SPA into the smaller fragment UI in the main SPA, by making the appropriate request to UIManager.
8. Service registry supports marking endpoints, commands etc. as Obsolete, deprecated, available etc. and should provide a runtime 'notification/warning' to the user as part of the dynamic developer documentation. This covers up the non-availability of full compile time code verification due to the decoupled architecture.