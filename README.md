# Edemly

Edemly - десктопний месенджер на .NET 8, WPF, ASP.NET Core, SignalR, Entity Framework Core та MySQL.

Папки і solution називаються `uchat`, бо це була технічна назва завдання. Назва самого застосунку - **Edemly**.

## Зміст

- [Про проєкт](#про-проєкт)
- [Можливості](#можливості)
- [Вимоги](#вимоги)
- [Налаштування](#налаштування)
- [Порядок запуску](#порядок-запуску)
- [Примітки](#примітки)
- [Розробники](#розробники)

## Про проєкт

Рішення складається з двох проєктів:

- `uchat_server` - backend API, SignalR hubs, EF Core міграції та робота з MySQL.
- `uchat` - WPF desktop client.

## Можливості

- Вхід через email-код та JWT-сесії
- Real-time чати, файли, аватари та голосові дзвінки
- Multi-tenant режим компаній з окремими базами даних
- Нотатки, нагадування та платежі
- Swagger/OpenAPI для перевірки backend API

## Вимоги

- Windows
- .NET 8 SDK
- MySQL Server 8 або сумісний сервер
- EF Core CLI:

```powershell
dotnet tool install --global dotnet-ef
```

## Налаштування

Перед запуском сервера змініть `uchat_server/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Port=3306;Database=uchat;User Id=root;Password=securepass;"
  },
  "AdminEmail": "admin@edemly.local",
  "Brevo": {
    "ApiKey": "MOCK_MODE"
  }
}
```

Створіть основну базу даних:

```sql
CREATE DATABASE uchat CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
```

Деталі по базі даних: [uchat_server/DATABASE_SETUP.md](uchat_server/DATABASE_SETUP.md).

## Порядок запуску

З кореня репозиторію:

```powershell
dotnet restore uchat.sln
dotnet build uchat.sln
```

Застосуйте міграцію основної бази:

```powershell
cd uchat_server
dotnet ef database update --context ServerDbContext
```

Запустіть сервер:

```powershell
dotnet run -- 8100
```

У другому терміналі запустіть клієнт:

```powershell
cd uchat
dotnet run -- http://localhost:8100
```

Swagger у development-режимі:

```text
http://localhost:8100/swagger
```

## Примітки

- Порт сервера обов'язковий: `dotnet run -- 8100`.
- URL сервера для клієнта обов'язковий: `dotnet run -- http://localhost:8100`.
- Якщо `Brevo:ApiKey` має значення `MOCK_MODE`, коди входу виводяться в консоль сервера.
- Під час запуску сервер також застосовує pending migrations і tenant-міграції для вже створених компаній.
- Ярлик на робочому столі необов'язковий і вимкнений за замовчуванням.
- Кеш і конфіг клієнта зберігаються в `%APPDATA%\uchat`.

Додаткові деталі: [docs/SETUP.md](docs/SETUP.md).

## Розробники

| Учасник | Роль і внесок |
|---|---|
| [Руслан Зуб](https://github.com/ReedSnake) | Team Lead & Full-Stack Developer. Забезпечив повний цикл розробки продукту: спроєктував архітектуру системи, реалізував ключові компоненти клієнтської та серверної частин, а також заклав основу бази даних. Окрім технічного лідерства, відповідав за управління проєктом: розподіляв завдання, координував процеси розробки та налаштовував інтеграцію сторонніх сервісів. |
| [Анастасія Лошакова](https://github.com/darkkfairy1) | Розробила візуальну концепцію застосунку та створила базові макети інтерфейсу. Відповідала за впровадження графічних рішень у клієнтську частину та оптимізацію UI на основі користувацького досвіду. |
| [Ростислав Ніколенко](https://github.com/NikolenkoRostislav) | Backend developer. Спільно працював над розробкою серверної логіки та REST API для взаємодії з клієнтом. Брав участь у проєктуванні структури бази даних, забезпеченні обробки запитів та оптимізації серверних процесів. |
| [Анастасія Власюк](https://github.com/AnastasiiaVlasiuk) | UI/UX designer. Зосередилася на проєктуванні взаємодії (UX) та підтримці єдиного візуального стилю. Забезпечувала узгодження дизайну з технічними можливостями реалізації та розробляла логіку інтерактивних елемент. |
