# Налаштування проєкту

Цей файл містить додаткові деталі для запуску Edemly. Назви папок `uchat` і `uchat_server` залишені як технічні назви проєктів.

## Зміст

- [Проєкти](#проєкти)
- [Конфігурація](#конфігурація)
- [База даних](#база-даних)
- [Логування](#логування)
- [Локальні файли](#локальні-файли)
- [Режим компанії](#режим-компанії)

## Проєкти

- `uchat_server/server.csproj` - ASP.NET Core backend.
- `uchat/client.csproj` - WPF client.

## Конфігурація

Основні налаштування сервера знаходяться у `uchat_server/appsettings.json`.

Мінімально потрібно перевірити:

- `ConnectionStrings:DefaultConnection` - підключення до MySQL.
- `Jwt:Key` - секрет для JWT.
- `AdminEmail` - email адміністратора.
- `Brevo:ApiKey` - `MOCK_MODE` для локальної перевірки або реальний ключ Brevo.

## База даних

Backend використовує два EF Core контексти:

- `ServerDbContext` - основна база `uchat`.
- `CompanyDbContext` - tenant-бази компаній.

Створіть основну базу вручну:

```sql
CREATE DATABASE uchat CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
```

Застосуйте міграції:

```powershell
cd uchat_server
dotnet ef database update --context ServerDbContext
```

Створення нових міграцій після зміни моделей:

```powershell
cd uchat_server
dotnet ef migrations add MigrationName --context ServerDbContext
dotnet ef migrations add MigrationName --context CompanyDbContext -o Migrations/CompanyDbMigrations
```

Tenant-міграції застосовуються автоматично під час створення компаній і під час запуску сервера для вже наявних компаній.

Детальніше: [../uchat_server/DATABASE_SETUP.md](../uchat_server/DATABASE_SETUP.md).

## Логування

Щоб зменшити вивід у консоль, змініть рівні логування в `uchat_server/appsettings.json`:

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

Для локальної перевірки можна залишити `Information`, бо в mock-режимі email-коди виводяться в консоль сервера.

## Локальні файли

Конфіг клієнта:

```text
%APPDATA%\uchat\config.json
```

Кеш клієнта:

```text
%APPDATA%\uchat\cache\profile_pictures\<company-or-personal>
%APPDATA%\uchat\cache\files\<company-or-personal>
```

Необов'язковий ярлик на робочому столі:

```text
%USERPROFILE%\Desktop\Edemly.lnk
```

Завантажені файли сервера:

```text
uchat_server/wwwroot/uploads
```

## Режим компанії

Запуск клієнта для tenant-компанії:

```powershell
dotnet run -- http://localhost:8100/company_name
```

або:

```powershell
dotnet run -- http://localhost:8100 --tenant company_name
```