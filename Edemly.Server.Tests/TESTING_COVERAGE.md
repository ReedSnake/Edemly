# Server Test Coverage Tracker

This file records what server behavior is covered, what remains uncovered, and which red tests intentionally describe desired behavior.

## Update Rules

- Update this tracker when adding or changing server tests.
- Keep test names in the same behavior format: `Action_Should_Result_When_Condition`.
- If a test fails because production code does not match the desired behavior, keep the test and list it under `Red Specifications`.
- Move items from `Backlog` to `Covered` when the behavior has an automated test.

## Current Status

- Branch: `test/server-test-coverage`
- Test command: `dotnet test Edemly.Server.Tests\Edemly.Server.Tests.csproj`
- Current result: `20 passed`
- Last verified: `2026-06-03`

## Test Infrastructure

Covered:

- `Program` can be hosted by `WebApplicationFactory<Program>`.
- `CustomWebApplicationFactory` starts the server in `Testing` environment.
- `ServerDbContext` is replaced with SQLite in-memory.
- Startup migrations and seeding can be disabled for tests.
- Background maintenance worker is removed from test host.
- `TestEmailService` captures verification codes for HTTP auth flows.
- `TestAuthHelper` can register, login, return JWT tokens, and create authorized clients.
- `TestHttpClientExtensions` adds `Authorization: Bearer <token>`.
- `TestChatHelper` creates private chats and messages through HTTP.

Covered tests:

- `Server_Should_Start`

## Auth Integration

Covered tests:

- `Register_Should_Create_User_When_Request_Is_Valid`
- `Register_Should_Return_BadRequest_When_Email_Already_Exists`
- `Register_Should_Return_BadRequest_When_Request_Is_Invalid`
- `Login_Should_Return_Token_When_Credentials_Are_Valid`
- `Login_Should_Return_Unauthorized_When_Password_Is_Wrong`
- `Login_Should_Return_Unauthorized_When_Email_Does_Not_Exist`
- `Login_Should_Not_Return_Password_Or_PasswordHash`
- `Login_Should_Return_BadRequest_When_Request_Is_Invalid`
- `Protected_Endpoint_Should_Return_Unauthorized_Without_Token`
- `Protected_Endpoint_Should_Return_Success_With_Valid_Token`

Notes:

- Auth is currently verification-code based. `Password_Is_Wrong` means wrong verification code until password auth exists.
- Register tests verify user persistence through the SQLite test database.

## Chat Integration

Covered tests:

- `CreatePrivateChat_Should_Create_Chat_When_Users_Exist`
- `CreatePrivateChat_Should_Return_Unauthorized_Without_Token`
- `GetMyChats_Should_Return_Only_User_Chats`
- `GetChat_Should_Return_Forbidden_When_User_Is_Not_Member`

## Message Integration

Covered tests:

- `SendMessage_Should_Create_Message_When_User_Is_Chat_Member`
- `SendMessage_Should_Return_Forbidden_When_User_Is_Not_Member`
- `GetMessages_Should_Return_Messages_In_Correct_Order`
- `DeleteMessage_Should_Return_Forbidden_When_User_Is_Not_Author`

## Red Specifications

None currently.

When a red test is intentionally kept as desired behavior, list it here with:

- Test name
- Expected behavior
- Current behavior
- Files likely needing changes

## Backlog

Server unit tests:

- `PermissionService` role matrix: base, admin, creator, unrelated user.
- `ChatMemberService` duplicate member prevention and role update/delete rules.
- `ChatService` self-chat rejection, missing user behavior, existing private chat reuse.
- `MessageService` pagination, update permissions, deletion side effects.
- JWT claim generation and malformed claim handling.
- File storage path validation and path traversal rejection.
- Payment status update behavior.

Server integration tests:

- Tenant path resolution and path rewrite.
- Tenant register/login/session-login flows.
- Tenant isolation for chats, messages, files, and users.
- Group chat creation and membership notifications.
- Chat member add/update/delete endpoints.
- User search, profile update, and delete workflows.
- File upload, download, delete, missing file, unauthorized access.
- Notes and remindings authorization.
- Payments initiate/history/status authorization.
- Admin company endpoints and role checks.

Database and migrations:

- `ServerDbContext` migration smoke test.
- `CompanyDbContext` migration smoke test.
- Unique indexes: login email, username, phone number, session token, chat membership.
- Master-only versus tenant-only table placement.

SignalR:

- Connect to `MainHub` with JWT.
- Reject unauthorized hub connections/actions.
- Deliver messages only to chat members.
- Group-created and group-updated notifications.

Client tests:

- Add `Edemly.Client.Tests` reference/setup for client API tests.
- `ApiService` URL building for personal and tenant modes.
- `ApiService` bearer token application.
- `ApiService` DTO parsing and failed response fallbacks.
- Cache key generation and cache invalidation.
- Language/config services where they do file or JSON handling.
