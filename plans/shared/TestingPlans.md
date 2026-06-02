# Edemly Testing Roadmap

## Phase 1. Add Test Projects

### Goal

Create a test foundation before risky refactoring begins.

### Current State

The solution currently contains only the application projects:

```text
Edemly.Server/Edemly.Server.csproj
Edemly.Client/Edemly.Client.csproj
```

### Proposed Projects

```text
tests/Edemly.Server.Tests
tests/Edemly.Client.Tests
```

### Tasks

* Add a server test project for API, services, permissions, tenant behavior, and file storage.
* Add a client test project for non-UI logic such as API clients, caches, DTO mapping, and helper services.
* Add both test projects to `Edemly.sln`.
* Use one test framework consistently, preferably xUnit.
* Add test naming conventions and folder structure.

### Result

There is a stable place to add regression tests before changing production code.

---

## Phase 2. Server Unit Tests

### Goal

Cover business rules that do not require a full HTTP server.

### Priority Areas

* `PermissionService`
* tenant-aware context resolution
* `ChatMemberService`
* `ChatService` private/group creation logic
* `FileStorageService` path validation and filename handling
* JWT claim generation
* payment status update behavior

### Tasks

* Use EF Core test databases where service logic depends on queries.
* Avoid mocking EF Core query behavior unless the code is simple.
* Test permission roles: base user, admin, creator, unrelated user.
* Test duplicate chat member prevention.
* Test malformed claim/user id handling.
* Test path traversal rejection for file operations.

### Result

Core server rules can be changed with confidence.

---

## Phase 3. Server Integration Tests

### Goal

Verify full API behavior through HTTP.

### Tasks

* Add `WebApplicationFactory`-based tests for ASP.NET Core endpoints.
* Move startup code into a testable shape if `Program.Main` blocks easy hosting.
* Use test configuration for JWT, email, file storage, and database.
* Test anonymous vs authorized access for users, messages, chat members, files, and admin endpoints.
* Test tenant path routing and path rewrite behavior.
* Test login/register/session flows in master and tenant modes.

### Result

Security-sensitive controller behavior is tested through the real request pipeline.

---

## Phase 4. Database and Migration Tests

### Goal

Catch schema mistakes before they reach production.

### Tasks

* Add migration smoke tests for `ServerDbContext`.
* Add migration smoke tests for `CompanyDbContext`.
* Verify expected indexes and constraints exist:
  * unique login email
  * unique username
  * unique chat membership `(chat_id, user_id)`
  * unique payment transaction id
  * unique session token
* Verify master-only and tenant-only tables are where they belong.
* Run schema checks against MySQL-compatible test infrastructure when possible.

### Result

Database refactors are verified beyond just compiling.

---

## Phase 5. File Storage Tests

### Goal

Support both local storage and MinIO/S3-compatible storage safely.

### Tasks

* Add contract tests for `IFileStorageService`.
* Run the same behavior tests against local filesystem storage and a fake or test MinIO implementation.
* Test upload, download, delete, missing file, invalid path, and tenant isolation.
* Test object key generation for tenant files.
* Test private file access through API proxy or signed URL generation, depending on the chosen design.

### Result

Local and production file storage behave the same from the application point of view.

---

## Phase 6. SignalR Tests

### Goal

Verify realtime behavior without relying only on manual checks.

### Tasks

* Add integration tests for connecting to `MainHub` with JWT.
* Test tenant-scoped user identifiers.
* Test message delivery only to chat members.
* Test group-created and group-updated notifications.
* Test rejection of unauthorized hub actions.

### Result

Realtime chat behavior stays correct during tenant and permission refactors.

---

## Phase 7. Client Tests

### Goal

Cover client logic that can be tested without full WPF UI automation.

### Tasks

* Extract API client logic behind interfaces where needed.
* Test request URL building for personal and tenant modes.
* Test DTO parsing and backward compatibility.
* Test cache key generation and cache invalidation logic.
* Test language/config services where they do file or JSON handling.
* Keep visual and WPF interaction testing manual until the code is easier to isolate.

### Result

Client-side regressions are caught where automated tests are practical.

---

## Phase 8. CI Quality Gate

### Goal

Make checks repeatable before merging or continuing large refactors.

### Tasks

* Add a CI workflow for build and tests.
* Run `dotnet build Edemly.sln --no-restore`.
* Run all test projects.
* Keep package restore warnings visible.
* Add a short checklist for manual verification where automated coverage is not ready.

### Result

Every change has a repeatable verification path.

---

## Phase 9. Feature Regression Suites

### Goal

Make new roadmap features testable from the start.

### Tasks

* Add invite-link tests for groups, calls, profiles, tenants, expiration, revoke, and unauthorized use.
* Add moderation tests for group bans, global bans, IP blocks, and rate limits.
* Add status tests for Online, Away, Do Not Disturb, Invisible, and Custom Status.
* Add messaging lifecycle tests for drafts, scheduled messages, auto-delete, polls, mentions, and pinned chats.
* Add company workspace tests for tasks, todos, calendar events, documentation permissions, and group threads.
* Add update-flow tests for version manifest parsing and update eligibility.

### Result

New features ship with clear regression coverage instead of becoming manual-only behavior.
