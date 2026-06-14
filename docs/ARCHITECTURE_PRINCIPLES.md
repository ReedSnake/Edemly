# Edemly Architecture Principles

Edemly should be structured enough to prevent the codebase from turning into chaos, but simple enough that one developer can realistically maintain it.

Architecture rules exist to keep responsibilities clear between parts of the system. They are not meant to force ceremony for its own sake.

## Programming Style

Edemly follows a pragmatic layered architecture.

The client uses WPF with MVVM where it helps UI state, binding, and view logic, combined with application, API, and infrastructure layers. Existing client code should be moved toward this model gradually. The goal is not to rewrite the entire client into a strict MVVM sample project.

The server uses layered architecture, also described as controller-service-infrastructure architecture:

* controllers and hubs are entry points;
* application services own use cases and business coordination;
* infrastructure implements technical mechanisms;
* data contains EF Core contexts, entities, migrations, and persistence configuration.

This is not strict Clean Architecture. Application services may coordinate EF Core contexts and infrastructure services directly when that keeps the implementation practical and understandable for the current project size.

## General Approach

Imperfect decisions are allowed when they:

* solve a real problem;
* do not create critical risk;
* do not break the main architectural direction;
* have a clear reason;
* can be improved later without rewriting the whole system.

Avoid abstractions that exist only to make the code look more architectural. Add an interface or boundary when it removes real coupling, improves testability, or protects a meaningful dependency direction.

## Client Layers

### Presentation

Presentation is responsible for the WPF UI layer:

* windows;
* pages;
* controls;
* bindings;
* user interaction;
* visual state;
* UI navigation;
* creating and activating WPF windows.

Presentation should not perform HTTP requests directly or contain complex business workflows.

### Application

Application is responsible for user-facing workflows:

* login;
* registration;
* loading chats;
* sending messages;
* updating application state;
* working with the current user;
* coordinating between Api, Infrastructure, and Presentation.

Application may coordinate a use case, but it should not depend directly on concrete WPF windows or UI controls.

### Api

Api is responsible for communication with the server:

* HTTP requests;
* endpoint calls;
* transport DTO handling;
* basic server response handling.

API clients should not contain business workflows or directly control UI.

Client-side hub access may live here or in Infrastructure later, depending on which boundary keeps the code simpler and clearer.

### Infrastructure

Infrastructure is responsible for technical mechanisms:

* cache;
* SignalR;
* startup settings;
* configuration;
* local file storage;
* external technical services.

Infrastructure should not decide business workflows. It provides technical capabilities used by Application.

## Server Layers

### Api

Api is the external entry point into the server:

* HTTP requests;
* controllers;
* hubs;
* request and response mapping;
* authorization attributes;
* API-contract validation.

Api should not contain the main business logic. Its job is to receive a request, call Application, and return an HTTP or realtime response.

### Application

Application owns business use cases and system coordination:

* creating chats;
* sending messages;
* checking permissions;
* working with users;
* working with notes;
* working with reminders;
* coordinating database reads and writes;
* calling technical services through clear boundaries.

Application may work with data directly. Its responsibility is not merely to save data, but to complete a concrete system scenario.

### Infrastructure

Infrastructure owns technical implementation details:

* cache;
* background workers;
* file storage;
* tenancy infrastructure;
* external services;
* hosting-related logic;
* technical adapters.

Infrastructure should not contain the main business logic, but it may implement technical mechanisms that Application needs.

### Data

Data owns persistence structure and database access:

* DbContext classes;
* entities;
* migrations;
* database configuration;
* relationships;
* persistence model.

Data should not directly control business workflows.

## Architectural Technical Debt

Architectural technical debt is allowed when it is intentional and documented.

Architectural debt is acceptable when:

* the reason is clear;
* there is a concrete task or constraint;
* the risks are understood;
* the decision does not block future development;
* there is a reasonable way to improve it later.

When a rule is temporarily broken, document:

* which rule is broken;
* why it is acceptable now;
* what risk it creates;
* when or under what condition it should be fixed.

Example:

Application temporarily coordinates call-window creation because stabilizing call restore flow is more important right now. This breaks the rule that Application should not depend on concrete WPF UI, so later window creation and activation should move back into Presentation.

## Related Documents

* [Client Architecture](client/ARCHITECTURE.md)
* [Server Architecture](server/ARCHITECTURE.md)
* [Shared Contracts](shared/CONTRACTS.md)
