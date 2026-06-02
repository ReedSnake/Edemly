# Server Tests Structure

This project is reserved for server-side testing of the Edemly solution.

## Goals

- Test the server through a realistic ASP.NET Core host.
- Use an in-memory database for repeatable test runs.
- Initialize and dispose test settings automatically.
- Validate tenant-aware behavior and isolation.
- Keep unit and integration tests separated by purpose.

## Current layout

```text
Edemly.Server.Tests/
  Infrastructure/
    CustomWebApplicationFactory.cs
  Integration/
    Auth/
    Chats/
    Health/
    Messages/
    Tenancy/
    Users/
  Unit/
    Helpers/
    Middleware/
    Services/
  Fixtures/
  TestData/
    AuthTestData.cs
  Utilities/
    TestAuthHelper.cs
    TestChatHelper.cs
    TestHttpClientExtensions.cs
```

## Notes

- See `TESTING_COVERAGE.md` for covered behavior, red specifications, and backlog.
- `Infrastructure` is for host setup, database setup, and tenant setup.
- `Integration` is for controller, middleware, and full request pipeline tests.
- `Unit` is for isolated service logic without a full host.
- `Fixtures` should manage shared lifecycle objects for the test host and database.
- `TestData` should store reusable seed data and builders.
- `Utilities` should contain helper assertions and random data builders.
- Failing tests are allowed when they describe desired behavior. Record them in `TESTING_COVERAGE.md` under `Red Specifications`.

## Recommended test approach

- Prefer SQLite in-memory over EF Core InMemory for relational behavior.
- Use one shared test host per test collection when practical.
- Keep tenant context and database state isolated between tests.
- Add tests incrementally, starting with tenant resolution, permissions, and authentication.
- Keep tests at HTTP level for endpoint behavior unless the behavior is clearly service-only.
