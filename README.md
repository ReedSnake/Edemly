<div align="center">

# Edemly

Desktop messenger platform built with .NET 10, WPF, ASP.NET Core, SignalR, Entity Framework Core, and MySQL.

</div>

## Contents

* [Technology Stack](#technology-stack)
* [Core Functionality](#core-functionality)
* [Projects](#projects)
* [Repository Structure](#repository-structure)
* [Documentation](#documentation)
* [Requirements](#requirements)
* [Configuration](#configuration)
* [Build and Run](#build-and-run)
* [Runtime Notes](#runtime-notes)
* [Team](#team)

## Technology Stack

| Category       | Technologies                                          |
| -------------- | ----------------------------------------------------- |
| Backend        | .NET 10, ASP.NET Core, Entity Framework Core, SignalR |
| Client         | WPF, XAML                                             |
| Database       | MySQL                                                 |
| Authentication | JWT                                                   |
| Testing        | NUnit, SQLite In-Memory                               |

## Core Functionality

* Private and group chats.
* Realtime messaging.
* Direct and group voice calls.
* File attachments and avatars.
* Email-code login with JWT sessions.
* Company workspaces with isolated tenant databases.
* Notes and reminders.
* Payments and premium subscriptions.

## Projects

| Project                                       | Description                                                                                        |
| --------------------------------------------- | -------------------------------------------------------------------------------------------------- |
| [`Edemly.Server`](Edemly.Server/)             | ASP.NET Core backend, REST API, SignalR hubs, tenant management, file storage, and database access |
| [`Edemly.Gateway`](Edemly.Gateway/)           | Local reverse proxy for API, uploads, payment callbacks, and SignalR hub routing                   |
| [`Edemly.Client`](Edemly.Client/)             | WPF desktop client application                                                                     |
| [`Edemly.Contracts`](Edemly.Contracts/)       | Shared DTO contracts used by the server and client                                                 |
| [`Edemly.Server.Tests`](Edemly.Server.Tests/) | Server-focused automated tests                                                                     |
| [`Edemly.Client.Tests`](Edemly.Client.Tests/) | Client-focused automated tests                                                                     |
| [`docs`](docs/)                               | Technical documentation                                                                            |

## Repository Structure

```text
Edemly.Server/
Edemly.Gateway/
Edemly.Client/
Edemly.Contracts/
Edemly.Server.Tests/
Edemly.Client.Tests/
deployment/local/
docs/
```

## Documentation

| Section                                       | Description                                                                                          |
| --------------------------------------------- | ---------------------------------------------------------------------------------------------------- |
| [Documentation Home](docs/README.md)          | Main entry point for project documentation                                                           |
| [Server Documentation](docs/server/README.md) | Backend architecture, API, authentication, database, realtime communication, testing, and deployment |
| [Client Documentation](docs/client/README.md) | Desktop client architecture and implementation                                                       |
| [Shared Documentation](docs/shared/README.md) | Shared contracts and communication conventions                                                       |

## Requirements

* Windows
* .NET 10 SDK
* MySQL Server 8 or compatible MySQL server
* EF Core CLI
* Optional: Docker Desktop for the staged local deployment profile

Install the EF Core CLI if needed:

```powershell
dotnet tool install --global dotnet-ef
```

## Configuration

Before starting the server, review `Edemly.Server/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Port=3306;Database=edemly;User Id=root;Password=securepass;"
  },
  "AdminEmail": "admin@edemly.local",
  "Brevo": {
    "ApiKey": "MOCK_MODE"
  }
}
```

Create the main database:

```sql
CREATE DATABASE edemly CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
```

For database details see:

* [Database Documentation](docs/server/DATABASE.md)

## Build and Run

Restore and build the solution:

```powershell
dotnet restore Edemly.sln
dotnet build Edemly.sln
```

Apply the master database migration:

```powershell
dotnet ef database update --project Edemly.Server --startup-project Edemly.Server --context ServerDbContext
```

Apply the tenant/company schema if needed:

```powershell
dotnet ef database update --project Edemly.Server --startup-project Edemly.Server --context CompanyDbContext
```

Start the server:

```powershell
dotnet run --project Edemly.Server -- 3500
```

Start the client in another terminal:

```powershell
dotnet run --project Edemly.Client -- http://localhost:3500
```

If SignalR hubs should use a different server or gateway endpoint, pass it explicitly:

```powershell
dotnet run --project Edemly.Client -- http://localhost:3500 --hub-server http://localhost:3500
```

Swagger is available in development mode:

```text
http://localhost:3500/swagger
```

## Local Deployment Profile

The staged local deployment lives under `deployment/local` and is intentionally split so local testing can continue without starting every service.

Start only infrastructure:

```powershell
docker compose -f deployment/local/docker-compose.yml up mysql minio static
```

Start the containerized server and gateway when needed:

```powershell
docker compose -f deployment/local/docker-compose.yml --profile server --profile gateway up --build gateway
```

With the gateway profile, the public entry point is:

```text
http://localhost:3500
```

The gateway routes `/main` and `/call` to a fixed hub upstream, routes payment callbacks to a fixed payment upstream, and keeps API/uploads separate so more server destinations can be added later.

The local static site is available at:

```text
http://localhost:8080
```

## Runtime Notes

* The server port argument is optional. If it is not passed, the server uses `PORT`, `ASPNETCORE_PORT`, or defaults to `8100`.
* The client server URL argument is still supported for local testing.
* The optional client `--hub-server` argument overrides the server used for SignalR hubs and is saved in `%APPDATA%\Edemly\config.json`.
* When `Brevo:ApiKey` is set to `MOCK_MODE`, login codes are printed to the server console.
* Local MinIO defaults are `edemly_admin` / `edemly_password` on ports `9000` and `9001`.
* Local payment URLs are controlled by `PublicBaseUrl`, `WayForPay:DomainName`, and `WayForPay:ReturnUrl`.
* Server startup automatically applies pending master migrations and tenant migrations for existing companies.
* Server migrations are stored in:

  * `Edemly.Server/Data/Migrations/ServerDb`
  * `Edemly.Server/Data/Migrations/CompanyDb`
* If you previously used the old migration chain, recreate local databases before applying the new migrations.
* New registrations may leave `Username`, `FirstName`, and `LastName` empty.
* `Username` must remain unique when set.
* The desktop shortcut is optional and disabled by default.
* Client configuration and cache files are stored in `%APPDATA%\Edemly`.

## Team

| Member                                                       | Responsibilities                                         |
| ------------------------------------------------------------ | -------------------------------------------------------- |
| [Ruslan Zub](https://github.com/ReedSnake)                   | Team Lead, Backend Development, Database Design, Testing |
| [Anastasiia Loshakova](https://github.com/darkkfairy1)       | Client Development and UI Implementation                 |
| [Rostislav Nikolenko](https://github.com/NikolenkoRostislav) | Backend Development                                      |
| [Anastasiia Vlasiuk](https://github.com/AnastasiiaVlasiuk)   | UI/UX Design                                             |
