# Client Architecture

This document describes the architecture of the Edemly WPF desktop client.

For the shared project-wide architecture rules, see [Edemly Architecture Principles](../ARCHITECTURE_PRINCIPLES.md).

## Overview

The Edemly client uses a pragmatic combination of MVVM and layered architecture.

MVVM is useful for WPF binding, view state, commands, and screen-specific interaction logic. Layered architecture keeps transport, application workflows, infrastructure mechanisms, and UI responsibilities separated.

The client should move toward this structure gradually. The goal is not to rewrite the whole application into strict MVVM, but to keep responsibilities clear whenever code is touched.

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

## Application

Application owns client use cases:

* login and registration coordination;
* current user/session state;
* loading chats and messages;
* sending messages;
* applying realtime updates to application state;
* coordinating cache, API clients, and UI-facing models.

Application should avoid direct dependencies on concrete WPF controls and windows. If a workflow temporarily needs WPF-specific behavior, the reason and future cleanup path should be documented.

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

## Related Documents

* [Client Documentation](README.md)
* [API Clients](API_CLIENTS.md)
* [Realtime](REALTIME.md)
* [Caching](CACHING.md)
* [Theming](THEMING.md)
