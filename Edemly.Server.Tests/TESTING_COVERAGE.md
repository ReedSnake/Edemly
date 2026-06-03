# Server Test Coverage Tracker

This file records what server behavior is covered, what remains uncovered, and which red tests intentionally describe desired behavior.

## Update Rules

- Update this tracker when adding or changing server tests.
- Keep test names in the same behavior format: `Action_Should_Result_When_Condition`.
- If a test fails because production code does not match the desired behavior, keep the test and list it under `Red Specifications`.
- Move items from `Backlog` to `Covered` when the behavior has an automated test.

## Current Status

- Branch: `refactor/auth-profile-redesign`
- Test command: `dotnet test Edemly.Server.Tests\Edemly.Server.Tests.csproj`
- Current result: `72 passed`
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
- `Register_Should_Return_Unauthorized_When_Verification_Code_Is_Invalid`
- `Register_Should_Create_Welcome_Chat_And_Membership`
- `Register_Should_Allow_Empty_Username`
- `Register_Should_Return_BadRequest_When_Username_Already_Exists`
- `Register_Should_Not_Derive_ProfileNames_From_Username`
- `Login_Should_Return_Token_When_Credentials_Are_Valid`
- `Login_Should_Return_Unauthorized_When_Password_Is_Wrong`
- `Login_Should_Return_Unauthorized_When_Email_Does_Not_Exist`
- `Login_Should_Not_Return_Password_Or_PasswordHash`
- `Login_Should_Return_BadRequest_When_Request_Is_Invalid`
- `SessionLogin_Should_Return_Token_When_SessionToken_Is_Valid`
- `SessionLogin_Should_Return_Unauthorized_When_SessionToken_Is_Invalid`
- `SessionLogin_Should_Return_Unauthorized_When_SessionToken_Is_Expired`
- `Logout_Should_Remove_Session_When_User_Is_Authenticated`
- `Protected_Endpoint_Should_Return_Unauthorized_Without_Token`
- `Protected_Endpoint_Should_Return_Success_With_Valid_Token`

Notes:

- Auth is currently verification-code based. `Password_Is_Wrong` means wrong verification code until password auth exists.
- Register tests verify user persistence through the SQLite test database.

## Auth Unit

Covered tests:

- `GetLoginCode_Should_Return_BadRequest_When_Request_Model_Is_Missing`
- `GetLoginCode_Should_Return_BadRequest_When_Email_Format_Is_Invalid`
- `GetLoginCode_Should_Resolve_Company_From_HttpContext_Items`
- `GetLoginCode_Should_Resolve_Company_From_RequestPath_When_TenantPrefix_Is_Present`
- `GetLoginCode_Should_Return_ServerError_When_TenantAllowlistLookup_Fails`
- `GetLoginCode_Should_Return_ServerError_When_EmailService_Throws`
- `Login_Should_Request_Admin_Token_When_Admin_Email_Matches_Configuration`
- `Logout_Should_Return_Unauthorized_When_UserIdClaim_Is_Invalid`

Helper coverage:

- `TestEmailService_Should_Treat_Email_Case_Insensitively`
- `TestEmailService_Should_Invalidate_Code_After_Successful_Verification`

## Chat Integration

Covered tests:

- `CreatePrivateChat_Should_Create_Chat_When_Users_Exist`
- `CreatePrivateChat_Should_Return_Unauthorized_Without_Token`
- `GetMyChats_Should_Return_Only_User_Chats`
- `GetMyChats_Should_Use_Fallback_Name_When_Other_User_Clears_ProfileFields`
- `GetChat_Should_Return_Forbidden_When_User_Is_Not_Member`

## Chat Member Integration

Covered tests:

- `AddChatMember_Should_Add_User_When_Requester_Is_Admin`
- `AddChatMember_Should_Return_Forbidden_When_Requester_Is_Not_Admin`
- `UpdateChatMemberRole_Should_Update_Role_When_Requester_Is_Creator`
- `RemoveChatMember_Should_Remove_User_When_Requester_Is_Admin`
- `RemoveChatMember_Should_Return_Forbidden_When_Requester_Is_Not_Admin`
- `UpdateChatMemberRole_Should_Return_Forbidden_When_Requester_Targets_Self_As_Creator`
- `RemoveChatMember_Should_Return_Forbidden_When_Requester_Targets_Self_As_Creator`

## Message Integration

Covered tests:

- `SendMessage_Should_Create_Message_When_User_Is_Chat_Member`
- `SendMessage_Should_Return_Forbidden_When_User_Is_Not_Member`
- `GetMessages_Should_Return_Messages_In_Correct_Order`
- `DeleteMessage_Should_Return_Forbidden_When_User_Is_Not_Author`

## User Integration

Covered tests:

- `GetMe_Should_Return_Current_User_When_Token_Is_Valid`
- `GetMe_Should_Return_Unauthorized_Without_Token`
- `SearchUsers_Should_Return_Matching_Users`
- `SearchUsers_Should_Return_Unauthorized_Without_Token`
- `UpdateProfile_Should_Update_User_Data_When_Request_Is_Valid`
- `UpdateProfile_Should_Clear_Optional_Fields_When_Empty_Strings_Are_Provided`
- `UpdateProfile_Should_Return_BadRequest_When_Username_Is_Duplicate`
- `UpdateProfile_Should_Return_Unauthorized_Without_Token`
- `DeleteUser_Should_Remove_Current_User`
- `DeleteUser_Should_Return_Forbidden_When_Deleting_Another_User`
- `DeleteUser_Should_Return_Unauthorized_Without_Token`

## Tenant Resolution Integration

Covered tests:

- `TenantPath_Should_Rewrite_To_Protected_Endpoint_When_Company_Exists`
- `TenantPath_Should_Not_Rewrite_When_First_Segment_Does_Not_Match_Company`
- `TenantPath_Should_Resolve_Company_Case_Insensitively_When_Request_Uses_Tenant_Prefix`

## Tenant Auth Integration

Covered tests:

- `TenantRegister_Should_Create_User_In_Tenant_Database_When_Email_Is_Allowed`
- `TenantRegister_Should_Allow_Empty_Username_When_Email_Is_Allowed`
- `TenantRegister_Should_Return_BadRequest_When_Email_Is_Not_Allowed`
- `TenantRegister_Should_Create_Welcome_Chat_And_Membership`
- `TenantLogin_Should_Return_Token_When_User_Exists_In_Tenant_Database`
- `TenantLogin_Should_Return_Unauthorized_When_User_Does_Not_Exist_In_Tenant_Database`
- `TenantSessionLogin_Should_Return_Token_When_SessionToken_Is_Valid_For_Tenant`
- `TenantSessionLogin_Should_Return_Unauthorized_When_SessionToken_Is_Invalid_For_Tenant`
- `TenantLogout_Should_Remove_Session_When_User_Is_Authenticated`
- `TenantGetCode_Should_Resolve_Company_From_QueryParameter_When_Tenant_Is_Provided`

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

- Tenant isolation for chats, messages, files, and users.
- Group chat creation and membership notifications.
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
