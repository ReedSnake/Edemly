# Edemly Tenant and Auth Roadmap

## Phase 1. Add Tenant Identity to Auth

### Goal

Make authenticated requests unambiguously tied to a tenant or master workspace.

### Current Issue

JWT tokens contain `userId`, username, email, and role, but no tenant/company identifier.

### Tasks

* Add tenant/company claim to JWT tokens for tenant users.
* Include a master/workspace claim for non-tenant users.
* Validate that request tenant matches token tenant.
* Reject requests where path/query tenant conflicts with token tenant.
* Add integration tests for cross-tenant access attempts.

### Result

A token from one workspace cannot be used against another workspace by changing the URL.

---

## Phase 2. Centralize Tenant Resolution

### Goal

Remove duplicate tenant lookup logic from middleware, controllers, hubs, and services.

### Tasks

* Keep tenant path parsing in one middleware or one resolver service.
* Replace controller-specific methods like `ResolveCompanyFromRequest` with a shared abstraction.
* Avoid fallback behavior that silently uses master DB after tenant resolution errors.
* Return a clear error when a tenant path is invalid or tenant DB is unavailable.
* Cache resolved company metadata safely for the request scope.

### Result

All request paths resolve tenant state the same way.

---

## Phase 3. Fix Tenant DbContext Lifetime

### Goal

Prevent disposed contexts and partial operations in tenant mode.

### Current Issue

Services store a resolved `DbContext` in a field and dispose it inside each method.

### Tasks

* Do not store tenant `DbContext` as a long-lived service field.
* Create context per operation or register a request-scoped tenant-aware context provider.
* Remove `if (_isTenant) _ctx.Dispose()` from service methods.
* Ensure services that call other services share the same operation scope or transaction when needed.
* Add regression tests for methods that call multiple service methods in one request.

### Result

Tenant service calls no longer fail after the first method disposes the context.

---

## Phase 4. Fix SignalR Tenant Isolation

### Goal

Prevent SignalR user routing conflicts across tenants.

### Current Issue

SignalR `Clients.User(...)` and `Clients.Users(...)` route by numeric `userId` only.

### Tasks

* Change SignalR user identifier to include tenant, for example `tenantName:userId`.
* Add tenant claim support to `JwtUserIdProvider`.
* Update all server broadcasts to use the tenant-scoped user identifier.
* Update client connection setup to pass tenant context consistently.
* Test two tenants with the same numeric user id connected at the same time.

### Result

Realtime notifications and messages are delivered only within the correct tenant.

---

## Phase 5. Rework Session Login

### Goal

Make refresh/session login tenant-aware and unambiguous.

### Tasks

* Store tenant identifier with session records or encode it into session tokens.
* Verify session tenant matches request tenant.
* Decide whether tenant sessions live only in tenant DB or in a centralized master session table.
* Standardize claim extraction across controllers.
* Consider adding `ClaimTypes.NameIdentifier` in JWT to match existing controller usage.

### Result

Session login cannot accidentally authenticate the wrong workspace user.

