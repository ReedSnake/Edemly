# Client UI Structure

This document describes how the WPF presentation layer is organized.

The UI is desktop-first. Presentation code may own WPF events, visual state, dialogs, windows, and navigation, while longer workflows should move into Application or Infrastructure services when they grow beyond view coordination.

## Contents

* [Overview](#overview)
* [Presentation Folders](#presentation-folders)
* [Pages](#pages)
* [Windows](#windows)
* [Dialogs and Controls](#dialogs-and-controls)
* [Rendering Helpers](#rendering-helpers)
* [Chat Workspace](#chat-workspace)
* [Navigation](#navigation)
* [Current Limitations](#current-limitations)
* [Related Documents](#related-documents)

## Overview

Presentation code lives under:

```text
Edemly.Client/Presentation
```

This folder contains WPF pages, windows, controls, dialogs, view models, shared resources, rendering helpers, and presentation controllers.

The older generic `Pages` approach has been moving toward feature folders and smaller partial classes. New UI work should follow the current `Presentation` structure instead of adding broad, mixed-responsibility files.

## Presentation Folders

| Folder | Purpose |
| ------ | ------- |
| `Behaviors` | reusable WPF behaviors |
| `Common` | shared WPF base classes and small common helpers |
| `Controls` | reusable controls such as `ConnectionStatusBar` |
| `Controllers` | presentation controllers that coordinate complex UI state |
| `Dialogs` | modal UI such as message boxes and attachment previews |
| `Pages` | routed application pages |
| `Rendering` | code-created UI rendering helpers |
| `Resources` | XAML resources, styles, colors, brushes, fonts |
| `ViewModels` | UI-facing models where binding state is useful |
| `Windows` | top-level WPF windows |

## Pages

Current page groups:

| Folder | Purpose |
| ------ | ------- |
| `Pages/Auth` | install, login, registration, verification |
| `Pages/Main` | chat workspace, group settings, contact info, attachments |
| `Pages/Calendar` | reminder/task calendar UI |
| `Pages/Settings` | profile, avatar, appearance, language, wallpaper |
| `Pages/Payments` | premium/payment UI |
| `Pages/Info` | about/support-style application information |

Large pages are split into partial classes by responsibility. Examples:

* `MainPage.Header.cs`
* `MainPage.Attachments.cs`
* `MainPage.GroupCalls.cs`
* `SettingsPage.Profile.cs`
* `CalendarPage.TaskPanels.cs`

Partial files should describe the UI responsibility they own. They should not become generic dumping grounds for unrelated event handlers.

## Windows

Top-level windows live under:

```text
Presentation/Windows
```

Important windows:

| Window | Purpose |
| ------ | ------- |
| `MainWindow` | application shell and page host |
| `CallWindow` | call UI, participant state, and media controls |

Windows inherit `ThemedWindow` when they need theme lifecycle behavior.

## Dialogs and Controls

Dialogs live under:

```text
Presentation/Dialogs
```

Current dialog areas:

* `AppMessageBox`
* attachment preview and file-picking dialogs

Reusable controls live under:

```text
Presentation/Controls
```

`ConnectionStatusBar` is the main shared connection-state control.

## Rendering Helpers

Rendering helpers live under:

```text
Presentation/Rendering
```

Current rendering areas:

| Folder | Purpose |
| ------ | ------- |
| `Rendering/Messages` | message bubbles, text/file/photo/voice/call-system rendering, context menus |
| `Rendering/Chats` | chat list item state and rendering |
| `Rendering/Common` | shared render helpers such as rich text links and styled context menus |

Rendering helpers are useful when UI elements are created dynamically and the logic is too detailed for a page code-behind file.

## Chat Workspace

The chat workspace is coordinated by:

```text
Presentation/Controllers/Chats
```

Important pieces:

| Component | Purpose |
| --------- | ------- |
| `ChatWorkspaceController.*` | chat list, current chat, realtime updates, presence, local mutations |
| `ChatWorkspaceState` | UI-facing workspace state |
| `ChatWorkspaceBindings` | explicit bindings between page controls and controller callbacks |
| `ChatListItemStateFactory` | creates chat-list item state from chat/user/status data |
| `ChatListItemBuilder` | renders chat-list items from precomputed state |

This controller still lives in Presentation because it coordinates WPF controls and visual state. Application services such as `ChatLoader` and attachment workflows handle reusable non-visual operations.

## Navigation

`MainWindow` hosts the application pages.

Startup resolves configuration and then navigates through install/auth/main flows based on saved state and authentication result.

External navigation, such as opening links or files through the OS, belongs in Infrastructure behind small services such as `IExternalNavigationLauncher`.

## Current Limitations

* Some pages are still large even after partial splitting.
* Some workflows still mix page coordination with application logic.
* `App.xaml.cs` remains the composition root and still exposes legacy static access while refactors continue.
* Dynamic UI code needs careful theme resource usage because it does not get XAML resource binding automatically.

## Related Documents

* [Client Architecture](ARCHITECTURE.md)
* [Theming](THEMING.md)
* [Realtime Communication](REALTIME.md)
* [Caching](CACHING.md)
