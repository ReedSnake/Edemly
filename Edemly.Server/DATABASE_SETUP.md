# Налаштування бази даних

Цей файл описує базу даних Edemly. Назва основної бази `uchat` залишена як технічна назва з початкового завдання.

## Зміст

- [Контексти бази даних](#контексти-бази-даних)
- [Початкове налаштування](#початкове-налаштування)
- [Міграції](#міграції)
- [Автоматична ініціалізація](#автоматична-ініціалізація)
- [Tenant-бази компаній](#tenant-бази-компаній)
- [Перевірка](#перевірка)

## Контексти бази даних

У backend є два EF Core контексти:

- `ServerDbContext` - основна база даних. Тут зберігаються користувачі, логіни, компанії, сесії та загальні дані.
- `CompanyDbContext` - tenant-база конкретної компанії. Тут зберігаються чати, повідомлення, нотатки, нагадування, платежі та дані компанії.

Міграції знаходяться тут:

```text
Edemly.Server/Migrations
Edemly.Server/Migrations/CompanyDbMigrations
```

## Початкове налаштування

1. Запустіть MySQL.

2. Створіть основну базу:

```sql
CREATE DATABASE uchat CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
```

3. Перевірте `ConnectionStrings:DefaultConnection` у `Edemly.Server/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Port=3306;Database=uchat;User Id=root;Password=securepass;"
  }
}
```

## Міграції

Застосувати міграції основної бази:

```powershell
cd Edemly.Server
dotnet ef database update --context ServerDbContext
```

Створити нову міграцію для основної бази:

```powershell
cd Edemly.Server
dotnet ef migrations add MigrationName --context ServerDbContext
```

Створити нову міграцію для tenant-баз:

```powershell
cd Edemly.Server
dotnet ef migrations add MigrationName --context CompanyDbContext -o Migrations/CompanyDbMigrations
```

## Автоматична ініціалізація

Під час запуску сервер:

- перевіряє підключення до MySQL;
- застосовує pending migrations для `ServerDbContext`;
- створює адміністратора з `AdminEmail`, якщо його ще немає;
- створює welcome chat;
- застосовує tenant-міграції для вже створених компаній.

Якщо `Brevo:ApiKey` дорівнює `MOCK_MODE`, email-коди для входу виводяться в консоль сервера.

## Tenant-бази компаній

Компанії мають окремі бази даних. Під час створення компанії backend створює tenant-базу і застосовує міграції `CompanyDbContext`.

Для запуску клієнта в межах компанії:

```powershell
dotnet run -- http://localhost:8100/company_name
```

або:

```powershell
dotnet run -- http://localhost:8100 --tenant company_name
```

## Перевірка

Перевірити, що основні таблиці створені:

```sql
SHOW TABLES;
```

Перевірити наявність адміністратора:

```sql
SELECT u.id, u.username, li.email
FROM user u
JOIN login_info li ON li.id = u.login_info_id;
```

Перевірити компанії:

```sql
SELECT id, name, db_name
FROM Companies;
```
