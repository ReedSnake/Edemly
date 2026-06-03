# Edemly Database Setup

This file describes the local database setup for Edemly.

## Database Contexts

The backend has two EF Core contexts:

- `ServerDbContext` - the main database. It stores users, login information, companies, sessions, and shared data.
- `CompanyDbContext` - a tenant database for one company. It stores chats, messages, notes, reminders, payments, and company-scoped data.

Migrations are stored in:

```text
Edemly.Server/Data/Migrations/ServerDb
Edemly.Server/Data/Migrations/CompanyDb
```

## Initial Setup

1. Start MySQL.

2. Create the main database:

```sql
CREATE DATABASE edemly CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
```

3. Check `ConnectionStrings:DefaultConnection` in `Edemly.Server/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Port=3306;Database=edemly;User Id=root;Password=securepass;"
  }
}
```

## Migrations

Apply the main database migration:

```powershell
dotnet ef database update --project Edemly.Server --startup-project Edemly.Server --context ServerDbContext
```

Create a new migration for the main database:

```powershell
dotnet ef migrations add MigrationName --project Edemly.Server --startup-project Edemly.Server --context ServerDbContext --output-dir Data/Migrations/ServerDb
```

Create a new migration for tenant databases:

```powershell
dotnet ef migrations add MigrationName --project Edemly.Server --startup-project Edemly.Server --context CompanyDbContext --output-dir Data/Migrations/CompanyDb
```

## Migration Reset

The repository now uses fresh initial migrations under `Data/Migrations`.

If your local MySQL databases were created from the older migration chain in `Edemly.Server/Migrations`, recreate them before applying the new initial migrations. For the main database that usually means:

```sql
DROP DATABASE IF EXISTS edemly;
CREATE DATABASE edemly CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
```

Tenant databases can also be recreated locally if they were built from the removed migration history.

## Startup Initialization

When the server starts, it:

- checks the MySQL connection;
- applies pending migrations for `ServerDbContext`;
- creates the administrator from `AdminEmail` if needed;
- creates the welcome chat;
- applies tenant migrations for existing companies.

The current auth/profile schema allows empty `Username`, `FirstName`, and `LastName`; `Username` remains unique when provided.

When `Brevo:ApiKey` is `MOCK_MODE`, email verification codes are printed to the server console.

## Tenant Databases

Companies have separate databases. During company creation, the backend creates a tenant database and applies `CompanyDbContext` migrations.

Tenant database names use this format:

```text
edemly_company_<company_name>
```

Run the client for a company:

```powershell
dotnet run -- http://localhost:8100/company_name
```

or:

```powershell
dotnet run -- http://localhost:8100 --tenant company_name
```

## Verification

Check that the main tables exist:

```sql
SHOW TABLES;
```

Check the administrator:

```sql
SELECT u.id, u.username, li.email
FROM user u
JOIN login_info li ON li.id = u.login_info_id;
```

Check companies:

```sql
SELECT id, name, db_name
FROM Companies;
```
