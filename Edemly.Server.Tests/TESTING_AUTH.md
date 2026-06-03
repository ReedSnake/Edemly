# Auth Test Guide

This note explains how auth coverage is split so we can extend it without turning every auth change into a full end-to-end rewrite.

## Test Layers

`Integration/Auth`

- Covers public HTTP contracts for `get-code`, `register`, `login`, `session-login`, and `logout`.
- Verifies persistence side effects such as created users, sessions, welcome chat membership, and response payload safety.
- Should be the first place to add tests when an auth change is visible to API clients.

`Integration/Tenancy`

- Covers tenant-prefixed auth behavior and tenant-only persistence.
- Verifies allowlist checks, tenant session handling, and tenant-specific welcome chat membership.
- Use these tests when auth behavior depends on tenant routing or tenant databases.

`Unit/Auth`

- Covers controller branches that are hard or noisy to hit through the full HTTP pipeline.
- Focuses on request-resolution fallbacks, controlled failure paths, and token-generation decisions.
- Prefer this layer for edge cases that only need mocked dependencies and a small SQLite context.

## Current Auth Focus

Covered behavior includes:

- verification code request validation and tenant allowlist enforcement
- master and tenant registration
- generated username and display-name parsing
- welcome chat membership after registration
- login and session-login success and failure paths
- logout session removal
- admin token generation path
- helper behavior for `TestEmailService`

## Running Auth Tests

Run the full server suite:

```powershell
dotnet test Edemly.Server.Tests\Edemly.Server.Tests.csproj
```

Run auth-focused tests only:

```powershell
dotnet test Edemly.Server.Tests\Edemly.Server.Tests.csproj --filter "FullyQualifiedName~Auth"
```

## When Adding New Auth Tests

- Add an integration test when the change affects endpoint behavior, authorization, routing, or database side effects.
- Add a unit test when the change targets an internal branch, exception path, or dependency decision inside `AuthController`.
- Update `TESTING_COVERAGE.md` whenever a new auth scenario becomes covered or moves back to backlog.
