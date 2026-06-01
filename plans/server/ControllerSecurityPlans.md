# Edemly Controllers and Security Roadmap

## Phase 1. Lock Down Public Endpoints

### Goal

Ensure controllers expose only intentional public data.

### Tasks

* Add `[Authorize]` to `UserController.GetById` or return only public profile fields.
* Add `[Authorize]` and chat membership checks to `MessageController.GetById`.
* Add `[Authorize]` and permission checks to `ChatMemberController.GetMember`.
* Keep `AuthController` endpoints public only where needed.
* Add endpoint tests for anonymous access.

### Result

Anonymous users cannot enumerate users, chat members, messages, emails, or phone numbers.

---

## Phase 2. Fix Chat Permission Checks

### Goal

Make chat administration rules correct and consistent.

### Current Issue

`ChatController.UpdateChat` has a TODO instead of an admin/creator permission check.

### Tasks

* Require `CanUpdateChat` before updating chat name, description, or icon.
* Require creator/admin role for member management.
* Fix `PermissionService.CanUpdateChatMember` and `CanDeleteChatMember` to query by `UserId`, not by `ChatMember.Id`.
* Prevent admins from modifying creators unless explicitly allowed.
* Add tests for base user, admin, and creator permissions.

### Result

Only users with the correct chat role can modify chats and members.

---

## Phase 3. Standardize User Claim Handling

### Goal

Avoid inconsistent auth behavior between controllers.

### Tasks

* Create one helper or base method for authenticated user id extraction.
* Prefer `ClaimTypes.NameIdentifier` plus the existing `userId` claim during migration.
* Replace direct `int.Parse(...)` calls with safe parsing and consistent unauthorized responses.
* Update `PaymentController`, which currently reads `ClaimTypes.NameIdentifier`, while most other controllers read `userId`.

### Result

All controllers identify the current user the same way.

---

## Phase 4. Move Business Logic Out of Controllers

### Goal

Keep controllers thin and predictable.

### Tasks

* Split `AuthController` into authentication, registration, session, and tenant registration services.
* Move welcome-chat creation after registration into a dedicated service.
* Move generated payment result HTML out of `PaymentController`.
* Replace controller-local request DTO classes with DTO files under `Api/DTOs`.
* Keep controller actions focused on validation, authorization, service call, and response mapping.

### Result

Controllers become easier to test, review, and secure.

---

## Phase 5. Improve Error Handling

### Goal

Avoid leaking internal errors to clients.

### Tasks

* Stop returning raw exception messages from public API responses.
* Use structured error DTOs.
* Log detailed exceptions server-side only.
* Return consistent `400`, `401`, `403`, `404`, and `500` responses.
* Add global exception handling middleware.

### Result

API behavior is consistent and safer for production.

