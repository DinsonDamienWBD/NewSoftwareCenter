# Core Project Structure

This document outlines the key components, namespaces, and contracts within the `SoftwareCenter.Core` project.

## Namespaces

### `SoftwareCenter.Core.Commands`
- **ICommand.cs**: Marker interface for commands.

### `SoftwareCenter.Core.Events`
- **IEvent.cs**: Marker interface for domain events.

### `SoftwareCenter.Core.Modules`
- **IModule.cs**: The primary interface for all dynamically-loaded modules.

### `SoftwareCenter.Core.UI`
This namespace houses contracts for UI elements and related operations, not the UI Engine itself.

- **UIElement.cs**: Abstract base class for all UI controls. (Actual definition of UI elements in this project like `CreateElementCommand`, `UIElementRegisteredEvent` and `UIElement` record class.)
- **ElementType.cs**: Enum for different types of UI elements.
- **UIAccessControl.cs**: Class for managing UI element access.
- **ITemplateService.cs**: Interface for templating services.
- Other UI related Commands and Events are also defined here.