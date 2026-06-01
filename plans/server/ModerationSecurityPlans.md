# Edemly Moderation, Admin, and Security Roadmap

## Phase 1. Group Administration

### Goal

Give group owners and admins practical moderation tools.

### Features

* Invite by link.
* Approve or reject join requests.
* Promote and demote admins.
* Kick members.
* Ban members from a group.
* Mute members.
* Lock group.
* View moderation history.

### Tasks

* Formalize group roles: owner, admin, member, banned.
* Add permission checks for every group action.
* Add audit log records for moderation actions.
* Add UI for group admin actions.
* Add tests for each role and action.

### Result

Groups can be managed without direct database changes.

---

## Phase 2. Global User Administration

### Goal

Allow platform/company admins to manage abusive users.

### Features

* User ban.
* User suspension.
* Force logout.
* Disable invite creation.
* Restrict messaging.
* View login/device history.

### Tasks

* Add admin roles at platform and tenant levels.
* Add `UserModerationState` or equivalent entity.
* Enforce bans during login, session refresh, API calls, and SignalR connection.
* Add admin endpoints and client UI.
* Add audit logs for admin actions.

### Result

Admins can respond to abuse without manual intervention.

---

## Phase 3. IP Blocking and Rate Limiting

### Goal

Reduce spam, brute-force attempts, and abusive traffic.

### Tasks

* Add ASP.NET Core rate limiting middleware.
* Apply stricter limits to auth code requests, login, registration, file upload, and invite use.
* Add IP blocklist support.
* Add user/account-based rate limits in addition to IP limits.
* Add logging for rate-limit and block events.
* Add admin UI or config for blocks later.

### Result

High-volume abusive requests are slowed down or blocked.

---

## Phase 4. Login and Device Security

### Goal

Make account activity visible and controllable.

### Features

* Logout on all devices.
* Login history.
* Active device list.
* Notify active devices when a new login happens.
* Cloud password or second password for sensitive actions.

### Tasks

* Store sessions per device instead of one session per user if multi-device is required.
* Add session/device metadata: device name, IP, user agent, created at, last seen.
* Add revoke session endpoint.
* Add revoke all sessions endpoint.
* Send SignalR notification to active devices after login.
* Design cloud password storage and recovery carefully before implementation.

### Result

Users can understand and control account access.

---

## Phase 5. Request and Query Optimization

### Goal

Improve performance before adding heavier features.

### Tasks

* Identify slow endpoints with logging or profiling.
* Fix N+1 queries in chat list, user batch lookup, and message loading.
* Add pagination limits everywhere list endpoints exist.
* Add database indexes for common filters and sorts.
* Add caching only after correctness and invalidation rules are clear.
* Add performance tests for hot paths.

### Result

The app can scale better before collaboration features increase load.

---

## Phase 6. Security Hardening

### Goal

Reduce avoidable security risks.

### Tasks

* Stop returning raw exception messages to clients.
* Add centralized error handling.
* Validate all DTOs consistently.
* Add request size limits and content-type checks.
* Harden file uploads and downloads.
* Add tenant claim validation.
* Review CORS policy before production.
* Move production secrets out of `appsettings.json`.

### Result

The server is safer for real users and company workspaces.

