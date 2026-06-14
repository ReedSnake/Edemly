# Edemly Documentation

This directory contains the technical documentation for the Edemly project.

Documentation is organized by application area and is intended to describe the system architecture, implementation details, communication flow, testing approach, and deployment process.

## Contents

* [Architecture Principles](#architecture-principles)
* [Server Documentation](#server-documentation)
* [Client Documentation](#client-documentation)
* [Shared Documentation](#shared-documentation)

## Architecture Principles

Project-wide architecture principles are documented in [ARCHITECTURE_PRINCIPLES](ARCHITECTURE_PRINCIPLES.md).

The short version:

* Client: WPF with pragmatic MVVM and layered architecture.
* Server: layered architecture / controller-service-infrastructure architecture.
* The project favors clear responsibilities over strict Clean Architecture ceremony.
* Intentional architectural debt is allowed when the reason, risk, and cleanup path are understood.

## Server Documentation

Documentation related to the ASP.NET Core backend, database architecture, realtime communication, testing, and deployment.

| Document                                                       | Description                                             |
| -------------------------------------------------------------- | ------------------------------------------------------- |
| [README](server/README.md)                                     | Server documentation overview                           |
| [ARCHITECTURE](server/ARCHITECTURE.md)                         | Server architecture, layers, services, and request flow |
| [SAFE_OPTIMIZATION_BACKLOG](server/SAFE_OPTIMIZATION_BACKLOG.md) | Safe remaining server optimization tasks                |
| [API](server/API.md)                                           | REST API endpoints and behavior                         |
| [AUTH](server/AUTH.md)                                         | Authentication and authorization                        |
| [DATABASE](server/DATABASE.md)                                 | Database structure and multi-tenant architecture        |
| [FILE_STORAGE](server/FILE_STORAGE.md)                         | File upload and storage system                          |
| [REALTIME](server/REALTIME.md)                                 | SignalR and realtime communication                      |
| [TESTING](server/TESTING.md)                                   | Testing strategy and infrastructure                     |
| [DEPLOYMENT](server/DEPLOYMENT.md)                             | Deployment and environment configuration                |

## Client Documentation

Documentation related to the WPF desktop application.

| Document                               | Description                                    |
| -------------------------------------- | ---------------------------------------------- |
| [README](client/README.md)             | Client documentation overview                  |
| [ARCHITECTURE](client/ARCHITECTURE.md) | Client architecture and application structure  |
| [UI_STRUCTURE](client/UI_STRUCTURE.md) | Pages, dialogs, resources, and UI organization |
| [API_CLIENTS](client/API_CLIENTS.md)   | Communication with the server API              |
| [REALTIME](client/REALTIME.md)         | SignalR clients and realtime communication     |
| [CACHING](client/CACHING.md)           | Local caching and file storage                 |
| [THEMING](client/THEMING.md)           | Themes, resources, and styling                 |
| [TESTING](client/TESTING.md)           | Client testing                                 |

## Shared Documentation

Documentation shared between the server and client projects.

| Document                         | Description                                        |
| -------------------------------- | -------------------------------------------------- |
| [README](shared/README.md)       | Shared documentation overview                      |
| [CONTRACTS](shared/CONTRACTS.md) | Shared DTO contracts and communication conventions |

## Documentation Structure

```text
docs/
|-- README.md
|-- ARCHITECTURE_PRINCIPLES.md
|-- server/
|   |-- README.md
|   |-- ARCHITECTURE.md
|   |-- API.md
|   |-- AUTH.md
|   |-- DATABASE.md
|   |-- FILE_STORAGE.md
|   |-- REALTIME.md
|   |-- TESTING.md
|   |-- DEPLOYMENT.md
|   `-- SAFE_OPTIMIZATION_BACKLOG.md
|-- client/
|   |-- README.md
|   |-- ARCHITECTURE.md
|   |-- UI_STRUCTURE.md
|   |-- API_CLIENTS.md
|   |-- REALTIME.md
|   |-- CACHING.md
|   |-- THEMING.md
|   `-- TESTING.md
`-- shared/
    |-- README.md
    `-- CONTRACTS.md
```

## Related Resources

* [Project README](../README.md)
* [Server Project](../Edemly.Server/)
* [Client Project](../Edemly.Client/)
* [Contracts Project](../Edemly.Contracts/)
