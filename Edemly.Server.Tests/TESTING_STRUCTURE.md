# ServerTests Structure

This project is reserved for server-side testing of the Edemly solution.

## Goals

- Test the server through a realistic ASP.NET Core host.
- Use an in-memory database for repeatable test runs.
- Initialize and dispose test settings automatically.
- Validate tenant-aware behavior and isolation.
- Keep unit and integration tests separated by purpose.

## Planned layout

```text
ServerTests/
  Infrastructure/
	Database/
	Tenant/
  Integration/
	Auth/
	Files/
	Health/
	Tenants/
  Unit/
	Helpers/
	Middleware/
	Services/
  Fixtures/
  TestData/
  Utilities/
```

## Notes

- The project currently contains no real tests yet.
- `Infrastructure` is for host setup, database setup, and tenant setup.
- `Integration` is for controller, middleware, and full request pipeline tests.
- `Unit` is for isolated service logic without a full host.
- `Fixtures` should manage shared lifecycle objects for the test host and database.
- `TestData` should store reusable seed data and builders.
- `Utilities` should contain helper assertions and random data builders.

## Recommended test approach

- Prefer SQLite in-memory over EF Core InMemory for relational behavior.
- Use one shared test host per test collection when practical.
- Keep tenant context and database state isolated between tests.
- Add tests incrementally, starting with tenant resolution, permissions, and authentication.
