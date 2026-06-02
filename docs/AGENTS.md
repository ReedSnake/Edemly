# Edemly Agent Instructions

These instructions apply to this repository.

## Language

- Reply to the user in Ukrainian unless they ask for another language.

## Git Naming

- Use typed commit messages: `<type>(<scope>): <summary>`.
- Always include a meaningful scope when the change has a clear area, for example `client`, `server`, `contracts`, `auth`, `chat`, `payments`, `docs`, or `assets`.
- Use typed branch names: `<type>/<scope>-<short-description>`.
- Prefer these common types: `feat`, `fix`, `hotfix`, `bugfix`, `docs`, `refactor`, `perf`, `test`, `build`, `ci`, `chore`, `security`, `release`, `deps`, `infra`, `config`, `migration`, `wip`, `spike`, `revert`.
- Before committing code changes, run `dotnet build Edemly.sln` unless the user explicitly asks not to.
- Commit periodically after verified migration/refactoring batches.

## Contracts

- Shared client/server DTOs belong in `Edemly.Contracts`.
- Keep DTO names consistent with the existing pattern: `CreateNameDto`, `UpdateNameDto`, `DeleteNameDto`, `NameDto`, and specific response DTOs where useful.
- Do not leak server entity enums into `Edemly.Contracts`. Represent cross-project enum-like values as primitives such as `int` or `string`, then convert at the server boundary.
- Keep client-only cache models in the client unless they become part of an API or SignalR contract.

## Branch Safety

- Do not delete branches unless the user explicitly asks to delete them.
- Preserve uncommitted user changes with stash or another reversible method before pulling, switching, or merging.
