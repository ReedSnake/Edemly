# Edemly

Edemly is a Windows desktop messenger built with .NET 10, WPF, ASP.NET Core, SignalR, Entity Framework Core, and MySQL.

## Projects

- `Edemly.Server` - backend API, SignalR hubs, EF Core migrations, file storage, and MySQL access.
- `Edemly.Client` - WPF desktop client.
- `Edemly.Contracts` - shared DTO contracts used by the server and client.
- `Edemly.Server.Tests` - server test project.
- `Edemly.Client.Tests` - client test project.

## Project Structure

```text
Edemly.Contracts/      Shared DTOs grouped by feature area.
Edemly.Server/         ASP.NET Core API, SignalR hubs, EF Core data, migrations, tenant services.
Edemly.Client/         WPF desktop app, pages, helpers, API services, local cache models, assets.
Edemly.Server.Tests/   Server-focused tests.
Edemly.Client.Tests/   Client-focused tests.
docs/                  Setup and operational documentation.
plans/                 Planning notes and review checklists.
```

## Features

- Email-code login with JWT sessions.
- Real-time chats, file attachments, avatars, and voice calls.
- Company tenant mode with separate tenant databases.
- Notes, reminders, payments, and premium subscription flow.
- Swagger/OpenAPI in development mode.

## Requirements

- Windows.
- .NET 10 SDK.
- MySQL Server 8 or a compatible MySQL server.
- EF Core CLI:

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

Database details: [Edemly.Server/DATABASE_SETUP.md](Edemly.Server/DATABASE_SETUP.md).

## Build And Run

From the repository root:

```powershell
dotnet restore Edemly.sln
dotnet build Edemly.sln
```

Apply the main database migration:

```powershell
cd Edemly.Server
dotnet ef database update --context ServerDbContext
```

Start the server:

```powershell
dotnet run -- 8100
```

Start the client in another terminal:

```powershell
cd Edemly.Client
dotnet run -- http://localhost:8100
```

Swagger is available in development mode:

```text
http://localhost:8100/swagger
```

## Notes

- The server port argument is required: `dotnet run -- 8100`.
- The client server URL argument is required: `dotnet run -- http://localhost:8100`.
- When `Brevo:ApiKey` is `MOCK_MODE`, login codes are printed to the server console.
- Server startup applies pending master migrations and tenant migrations for existing companies.
- The desktop shortcut is optional and disabled by default.
- Client config and cache files are stored under `%APPDATA%\Edemly`.

Additional setup details: [docs/SETUP.md](docs/SETUP.md).

## Git Workflow

Use typed commit messages so every commit and branch says what area it touches.

Commit format:

```text
<type>(<scope>): <summary>
```

Examples:

```text
feat(auth): add email code verification
fix(chat): prevent duplicate message rendering
refactor(contracts): move message DTOs to shared project
docs(readme): document git workflow
```

Branch format:

```text
<type>/<scope>-<short-description>
```

Examples:

```text
feat/auth-email-verification
fix/chat-message-duplicates
refactor/contracts-message-dtos
docs/readme-git-workflow
```

Common types:

| Type | Use for |
|---|---|
| `feat` | New feature or user-facing capability |
| `fix` | Bug fix |
| `hotfix` | Urgent production fix |
| `bugfix` | Bug-fix branch name alternative |
| `docs` | Documentation |
| `refactor` | Code restructuring without behavior change |
| `perf` | Performance improvement |
| `test` | Tests and test infrastructure |
| `build` | Project files, build config, package references |
| `ci` | CI/CD workflow changes |
| `chore` | Maintenance work that does not fit another type |
| `security` | Security-related change |
| `release` | Release preparation or versioning |
| `deps` | Dependency updates |
| `infra` | Infrastructure or deployment support |
| `config` | Configuration changes |
| `migration` | Database/schema migrations |
| `wip` | Temporary work-in-progress branch only |
| `spike` | Short investigation or prototype branch |
| `revert` | Reverting a previous change |

Prefer scopes like `client`, `server`, `contracts`, `auth`, `chat`, `messages`, `companies`, `payments`, `notes`, `remindings`, `files`, `assets`, `docs`, and `tests`.

## Developers

| Contributor | Role |
|---|---|
| [Ruslan Zub](https://github.com/ReedSnake) | Team Lead and Full-Stack Developer |
| [Anastasiia Loshakova](https://github.com/darkkfairy1) | UI concept and client interface work |
| [Rostislav Nikolenko](https://github.com/NikolenkoRostislav) | Backend developer |
| [Anastasiia Vlasiuk](https://github.com/AnastasiiaVlasiuk) | UI/UX designer |
