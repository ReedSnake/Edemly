# Edemly Database Setup

This file describes the local database setup for Edemly.

## Database Contexts

The backend has two EF Core contexts:

- `ServerDbContext` - the main database. It stores users, login information, companies, sessions, and shared data.
- `CompanyDbContext` - a tenant database for one company. It stores chats, messages, notes, reminders, payments, and company-scoped data.

Migrations are stored in:

```text
Edemly.Server/Migrations
Edemly.Server/Migrations/CompanyDbMigrations
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
cd Edemly.Server
dotnet ef database update --context ServerDbContext
```

Create a new migration for the main database:

```powershell
cd Edemly.Server
dotnet ef migrations add MigrationName --context ServerDbContext
```

Create a new migration for tenant databases:

```powershell
cd Edemly.Server
dotnet ef migrations add MigrationName --context CompanyDbContext -o Migrations/CompanyDbMigrations
```

## Startup Initialization

When the server starts, it:

- checks the MySQL connection;
- applies pending migrations for `ServerDbContext`;
- creates the administrator from `AdminEmail` if needed;
- creates the welcome chat;
- applies tenant migrations for existing companies.

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
