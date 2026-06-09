# Server Architecture

This document describes the architecture of the Edemly server application.

The goal of this document is to explain how the backend is structured, which layers it contains, how requests move through the system, and how the server communicates with shared contracts, databases, infrastructure services, and realtime clients.

## Contents

* [Overview](#overview)
* [Technology Stack](#technology-stack)
* [Project Structure](#project-structure)
* [Architectural Layers](#architectural-layers)
* [Request Processing Model](#request-processing-model)
* [Tenant Database Resolution](#tenant-database-resolution)
* [Main Components](#main-components)
* [Design Notes](#design-notes)
* [Current Limitations](#current-limitations)
* [Related Documents](#related-documents)

## Overview

Edemly Server is an ASP.NET Core backend application responsible for REST APIs, SignalR realtime communication, authentication, authorization, file storage, database access, and company tenant management.

The server is used by the WPF desktop client and exposes both HTTP endpoints and realtime hub connections.

The backend follows a practical layered architecture. API controllers and SignalR hubs act as entry points, application services contain most of the business and use-case logic, infrastructure components handle technical concerns, and the data layer contains EF Core database contexts, entities, and migrations.

The project is not intended to represent strict Clean Architecture. Application services may directly coordinate EF Core contexts and infrastructure services where it keeps the implementation simpler and more practical for the current project size.

## Technology Stack

| Technology            | Purpose                             |
| --------------------- | ----------------------------------- |
| ASP.NET Core          | Backend application framework       |
| Entity Framework Core | Database access and migrations      |
| MySQL                 | Main relational database provider   |
| SignalR               | Realtime communication              |
| JWT                   | Authentication and authorization    |
| Swagger / OpenAPI     | API exploration in development mode |
| NUnit                 | Automated testing                   |
| SQLite In-Memory      | Integration test database provider  |

## Project Structure

```text
Edemly.Server/
├─ Api/
│  ├─ Controllers/
│  ├─ Hubs/
│  └─ Middleware/
│
├─ Application/
│  ├─ Auth/
│  ├─ Chats/
│  ├─ ChatMembers/
│  ├─ Common/
│  ├─ Companies/
│  ├─ Files/
│  ├─ Messages/
│  ├─ Notes/
│  ├─ Payments/
│  ├─ Remindings/
│  ├─ Users/
│  └─ Welcome/
│
├─ Configuration/
│
├─ Data/
│  ├─ Entities/
│  ├─ Migrations/
│  │  ├─ ServerDb/
│  │  └─ CompanyDb/
│  ├─ CompanyDbContext.cs
│  └─ ServerDbContext.cs
│
├─ Infrastructure/
│  ├─ Auth/
│  ├─ BackgroundServices/
│  ├─ Caching/
│  ├─ Email/
│  ├─ Files/
│  ├─ Hosting/
│  ├─ Payments/
│  ├─ Presence/
│  ├─ Realtime/
│  └─ Tenancy/
│
├─ Program.cs
└─ appsettings.json
```

The server project is separated from shared contracts and test projects:

```text
Edemly.Contracts/       Shared DTOs and contracts
Edemly.Server/          Backend application
Edemly.Server.Tests/    Server tests
```

## Architectural Layers

| Layer          | Responsibility                                                                                                   |
| -------------- | ---------------------------------------------------------------------------------------------------------------- |
| Api            | HTTP controllers, SignalR hubs, middleware, request entry points                                                 |
| Application    | Business services, use-case logic, permission checks, result models, and operation coordination                  |
| Infrastructure | Technical implementations such as JWT, email, file storage, payments, realtime notifications, tenancy, and cache |
| Data           | EF Core DbContexts, entities, migrations, and database configuration                                             |
| Contracts      | Shared DTOs stored in `Edemly.Contracts` and used by both server and client                                      |

The main idea is to keep controllers and hubs thin. They should receive requests, validate access where needed, call application services, and return consistent responses.

Business logic should live in application services, not directly in controllers.

## Request Processing Model

A typical request is processed in the following way:

1. The WPF client sends an HTTP request or a SignalR hub message.
2. ASP.NET Core middleware processes the request.
3. Authentication and authorization are applied where required.
4. Tenant information is resolved when the request is company-specific.
5. The controller or hub receives the request.
6. The controller or hub delegates the operation to an application service.
7. The application service validates business rules and checks permissions.
8. The application service coordinates database access or infrastructure services.
9. The service returns a service result.
10. The controller or hub converts the result into an HTTP or realtime response.

The controller or hub is responsible for request-specific behavior, while the service is responsible for the actual application operation.

Examples of API layer responsibilities:

* Receiving route, query, and body parameters.
* Requiring authenticated user identity where needed.
* Calling the correct application service method.
* Converting service results into HTTP or realtime responses.

Examples of application service responsibilities:

* Validating business rules.
* Checking permissions.
* Creating, updating, or deleting entities.
* Coordinating database and infrastructure operations.
* Returning consistent service result objects.

Authentication details are described in [AUTH.md](AUTH.md). API endpoint details are described in [API.md](API.md).

## Tenant Database Resolution

Edemly supports company workspaces with tenant-aware database access.

The server uses a main database for global application data and company metadata. Company-specific data can be stored in separate company databases.

Tenant-aware operations use dedicated tenancy infrastructure to determine which database context should be used for the current request or operation.

The general model is:

1. A request enters the server.
2. Tenant middleware attempts to resolve tenant information from the request.
3. The resolved tenant is stored in the current tenant context.
4. Application services use tenant-aware database resolution when needed.
5. Global operations use `ServerDbContext`.
6. Company-specific operations use `CompanyDbContext` for the selected company.

The important point is that a request does not use all company databases at once. The tenant resolution process determines whether the current operation should use the main database or a selected company database.

Detailed database structure, tenant provisioning, and migration strategy are described in [DATABASE.md](DATABASE.md).

## Main Components

| Component            | Responsibility                                                     |
| -------------------- | ------------------------------------------------------------------ |
| AuthService          | Authentication, login flow, verification codes, and token creation |
| UserService          | User profile and user-related operations                           |
| ChatService          | Chat creation, retrieval, and chat-level operations                |
| ChatMemberService    | Chat membership and member permissions                             |
| MessageService       | Message creation, retrieval, updates, and deletion                 |
| CompanyService       | Company workspace and tenant-related operations                    |
| FileStorageService   | File saving and public file access                                 |
| RemindingService     | Reminder-related operations                                        |
| PaymentService       | Payments and premium flow                                          |
| ChatRealtimeNotifier | Realtime chat notifications                                        |
| DbContextResolver    | Tenant-aware database context resolution                           |

## Design Notes

* Controllers are intended to stay thin and delegate business logic to application services.
* SignalR hubs are used for realtime communication and should not contain large business logic.
* Shared request and response DTOs are stored in `Edemly.Contracts`.
* Server responses use service result models to keep controller responses consistent.
* Database access is handled through EF Core contexts.
* Multi-tenant access is handled through dedicated tenancy services.
* Infrastructure-specific logic is separated from application services where possible.
* Server tests use a dedicated test project and isolated test infrastructure.
* The server follows practical layered architecture rather than strict Clean Architecture.

## Current Limitations

The current architecture is suitable for the current project size, but several areas may be improved later:

* `Program.cs` contains most application startup configuration and can be split into extension methods.
* Some application services directly coordinate infrastructure and EF Core dependencies.
* Tenant-related abstractions may need further separation if the multi-tenant model becomes more complex.
* Some admin endpoints should be reviewed to ensure authorization rules are applied consistently.
* Runtime upload folders should not contain committed user files in the repository.
* Development secrets and production secrets should be stored outside regular committed configuration files.

## Related Documents

* [Server Documentation](README.md)
* [API](API.md)
* [Authentication](AUTH.md)
* [Database](DATABASE.md)
* [Deployment](DEPLOYMENT.md)
* [File Storage](FILE_STORAGE.md)
* [Realtime](REALTIME.md)
* [Testing](TESTING.md)
