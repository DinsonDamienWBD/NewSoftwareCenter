# SoftwareCenter.UI.Engine - Project Structure

This document outlines the structure and purpose of the `SoftwareCenter.UI.Engine` project. This project acts as the bridge between the backend C# Kernel/Modules and the frontend JavaScript client. It translates UI requests from modules into SignalR messages that the browser can understand and render.

## Project Purpose

- **Implements `IUIEngine`**: Provides the concrete implementation for the `IUIEngine` interface defined in `SoftwareCenter.Core`.
- **Hosts SignalR Hub**: Contains the SignalR hub (`/ui-hub`) that the frontend connects to.
- **Translates Commands to Events**: Receives method calls (e.g., `CreateCard`) and broadcasts messages to the connected client(s) (e.g., `RenderCard`).

## Key Components

### `SoftwareCenter.UI.Engine.Hubs`
- **/UIHub.cs**: The main SignalR hub class. It exposes methods that the frontend client can listen to, such as `RenderNavButton`, `RenderContentContainer`, and `UpdateElementContent`.

### `SoftwareCenter.UI.Engine.Services`
- **/SignalRUIEngine.cs**: The concrete implementation of `IUIEngine`. This service is registered with the `IKernel`'s service locator. When a module calls `kernel.GetService<IUIEngine>().CreateCard(...)`, this class's method is invoked. It then uses the `IHubContext<UIHub>` to send the appropriate message to the client.

## Frontend Interaction (`site.js`)

The `wwwroot/js/site.js` file contains the client-side logic for the UI shell.
- It establishes a connection to the `/ui-hub`.
- It contains handlers (`connection.on(...)`) for each message type sent by the `UIHub` (e.g., `RenderNavButton`). These handlers manipulate the DOM to build the user interface dynamically.
- It captures user interactions (e.g., button clicks) and sends them back to the server via a standard HTTP POST to an API endpoint (`/api/ui/interact`), which then translates them into `ICommand` objects for the Kernel to process.