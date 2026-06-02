# Edemly Setup

This file contains additional local setup details for Edemly.

## Projects

- `Edemly.Server/Edemly.Server.csproj` - ASP.NET Core backend.
- `Edemly.Client/Edemly.Client.csproj` - WPF client.

## Configuration

Main server settings live in `Edemly.Server/appsettings.json`.

Review at least these values before running locally:

- `ConnectionStrings:DefaultConnection` - MySQL connection string.
- `Jwt:Key` - JWT signing secret.
- `Jwt:Issuer` - expected value: `edemly-server`.
- `Jwt:Audience` - expected value: `edemly-client`.
- `AdminEmail` - administrator email.
- `Brevo:ApiKey` - `MOCK_MODE` for local testing or a real Brevo key.

## Database

The backend uses two EF Core contexts:

- `ServerDbContext` - the main `edemly` database.
- `CompanyDbContext` - tenant databases for companies.

Create the main database manually:

```sql
CREATE DATABASE edemly CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
```

Apply migrations:

```powershell
cd Edemly.Server
dotnet ef database update --context ServerDbContext
```

Create new migrations after model changes:

```powershell
cd Edemly.Server
dotnet ef migrations add MigrationName --context ServerDbContext
dotnet ef migrations add MigrationName --context CompanyDbContext -o Migrations/CompanyDbMigrations
```

Tenant migrations are applied automatically when companies are created and when the server starts for existing companies.

More details: [../Edemly.Server/DATABASE_SETUP.md](../Edemly.Server/DATABASE_SETUP.md).

## Logging

To reduce console output, update logging levels in `Edemly.Server/appsettings.json`:

```json
"Logging": {
  "LogLevel": {
    "Default": "Warning",
    "Microsoft": "Warning",
    "Microsoft.AspNetCore": "Warning",
    "Microsoft.EntityFrameworkCore": "Warning"
  }
}
```

For local email-code testing, keep `Brevo:ApiKey` set to `MOCK_MODE`; verification codes are printed to the server console.

## Local Files

Client config:

```text
%APPDATA%\Edemly\config.json
```

Client cache:

```text
%APPDATA%\Edemly\cache\profile_pictures\<company-or-personal>
%APPDATA%\Edemly\cache\files\<company-or-personal>
```

Optional desktop shortcut:

```text
%USERPROFILE%\Desktop\Edemly.lnk
```

Server uploads:

```text
Edemly.Server/wwwroot/uploads
```

## Company Mode

Run the client for a tenant company:

```powershell
dotnet run -- http://localhost:8100/company_name
```

or:

```powershell
dotnet run -- http://localhost:8100 --tenant company_name
```
