# Client Architecture

This document describes the architecture of the Edemly WPF desktop client.

For the shared project-wide architecture rules, see [Edemly Architecture Principles](../ARCHITECTURE_PRINCIPLES.md).

## Contents

* [Overview](#overview)
* [Project Layers](#project-layers)
* [Presentation](#presentation)
* [Application](#application)
* [Api](#api)
* [Infrastructure](#infrastructure)
* [Startup and Composition](#startup-and-composition)
* [Main Components](#main-components)
* [Dependency Direction](#dependency-direction)
* [Practical Exceptions](#practical-exceptions)
* [Current Limitations](#current-limitations)
* [Related Documents](#related-documents)

## Overview

The Edemly client is a WPF desktop application with a pragmatic layered structure.

WPF patterns are used where they help with binding, view state, commands, and screen-specific interaction logic. The project is not trying to become strict MVVM. The main goal is to keep transport, workflows, infrastructure mechanisms, and UI responsibilities understandable whenever code is touched.

## Project Layers

| Layer          | Responsibility |
| -------------- | -------------- |
| Presentation   | WPF pages, windows, controls, dialogs, navigation, visual state, bindings, and user interaction |
| Application    | User workflows, state coordination, session-aware operations, and orchestration between UI, API, and infrastructure |
| Api            | HTTP transport, endpoint calls, request/response DTO handling, and server response parsing |
| Infrastructure | SignalR, cache, local storage, configuration, audio, notifications, filesystem, and OS integration |
| Contracts      | Shared transport contracts used by the client and server |

## Presentation

Presentation owns WPF-specific behavior:

* pages;
* windows;
* controls;
* dialogs;
* navigation;
* visual state;
* WPF events;
* bindings and commands;
* creating and activating WPF windows.

Presentation may call Application services to execute workflows. It should not perform HTTP requests directly or own multi-step server workflows.

Important presentation areas:

* `Presentation/Pages` for routed pages;
* `Presentation/Windows` for top-level windows;
* `Presentation/Dialogs` for modal UI;
* `Presentation/Controls` for reusable controls;
* `Presentation/Controllers` for complex UI coordination;
* `Presentation/Rendering` for code-created UI elements.

## Application

Application owns client use cases:

* login and registration coordination;
* current user/session state;
* loading chats and messages;
* sending messages;
* applying realtime updates to application state;
* coordinating cache, API clients, and UI-facing models.

Application should avoid direct dependencies on concrete WPF controls and windows. If a workflow temporarily needs WPF-specific behavior, the reason and future cleanup path should be documented.

Current Application areas include auth, session, chats, attachments, calls, health checks, localization, notes, users, and theme state.

## Api

Api owns communication with the server:

* endpoint routes;
* HTTP requests;
* DTO serialization and deserialization;
* basic transport error handling.

API clients should stay thin. They should not decide business workflows or directly update UI.

## Infrastructure

Infrastructure owns technical mechanisms:

* SignalR connections;
* local cache;
* file storage;
* configuration loading;
* audio and notification services;
* OS integration.

Infrastructure should provide capabilities to Application. It should not own the user-facing scenario.

Current Infrastructure areas include SignalR, caching, local storage, startup configuration, notifications, legal document loading, external navigation, audio, and attachment filesystem helpers.

## Startup and Composition

`App.xaml.cs` is the WPF startup and composition root.

Startup is responsible for:

1. loading local configuration;
2. resolving the API, hub, update, and static client config URLs;
3. initializing `ClientServiceRegistry`;
4. restoring saved authentication when possible;
5. setting API and cache authentication tokens;
6. connecting realtime services after authentication;
7. opening `MainWindow` and navigating to the correct page.

`ClientServiceRegistry` creates the shared API clients, `HubService`, `AuthService`, `ChatCache`, `ProfilePictureCache`, and `FileCache`.

The client still exposes some static access through `App.*` while refactors are in progress. New code should prefer explicit dependencies or small services when that keeps ownership clearer.

## Main Components

| Component | Responsibility |
| --------- | -------------- |
| `App.xaml.cs` | startup, composition, session restore, top-level app coordination |
| `ClientServiceRegistry` | creates API clients, realtime service, auth service, and caches |
| `ApiClients` | aggregates endpoint-specific HTTP clients |
| `ClientSessionManager` | current user/session coordination |
| `HubService` | SignalR main and call hub connections |
| `AppRealtimeCoordinator` | connection status and incoming-call app wiring |
| `ChatLoader` | loading chats and messages through API/cache |
| `ChatWorkspaceController` | chat UI state, realtime updates, and current-chat presentation |
| `CallSessionController` | call state transitions and hub call operations |
| `CallWindowCoordinator` | opening and focusing the call window |
| `ThemeService` | theme palette selection and application resources |
| `ConfigService` | local non-secret configuration |
| `SecureStorageService` | DPAPI-protected token storage |

## Dependency Direction

The preferred dependency direction is:

```text
Presentation -> Application -> Api
Presentation -> Application -> Infrastructure
Application  -> Contracts
Api          -> Contracts
Infrastructure -> Contracts
```

Avoid:

* Application depending on WPF types or concrete Presentation classes.
* Infrastructure directly controlling pages, windows, or UI state.
* Api containing business workflows.
* Application services resolving dependencies through global UI state when constructor dependencies or small ports would keep ownership clearer.

## Practical Exceptions

Some legacy areas may still use static access, direct page coordination, or mixed responsibilities. That is acceptable while the project is being stabilized.

When touching such code, prefer small behavior-preserving cleanup:

* remove unused code;
* move repeated logic into a local helper;
* keep API contracts stable;
* avoid broad rewrites;
* leave a clearer boundary than the one you found.

## Current Limitations

The current client structure is improving, but several areas still need careful handling:

* `App.xaml.cs` is still large and owns broad startup/application coordination.
* Some Presentation controllers coordinate both WPF controls and workflow-like behavior.
* Some Application services still depend on delegates or static access while composition is being cleaned up.
* Client automated test coverage is minimal.
* Realtime and call flows are behavior-sensitive and should be refactored in small, verified steps.

## Related Documents

* [Client Documentation](README.md)
* [UI Structure](UI_STRUCTURE.md)
* [API Clients](API_CLIENTS.md)
* [Realtime](REALTIME.md)
* [Caching](CACHING.md)
* [Theming](THEMING.md)
* [Testing](TESTING.md)
