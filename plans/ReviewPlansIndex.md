# Edemly Review Plans Index

## Purpose

This folder-level roadmap splits the architecture review into topic-specific plans.

## Plans

* [client/CallingPlans.md](client/CallingPlans.md) - calls roadmap.
* [client/ClientBugfixPlans.md](client/ClientBugfixPlans.md) - cache/display bugs, theme refresh, install/uninstall, updates, dialogs, sounds, and attachments.
* [server/DatabasePlans.md](server/DatabasePlans.md) - database structure, indexes, schema consistency, and migrations.
* [server/TenantAuthPlans.md](server/TenantAuthPlans.md) - tenant resolution, JWT claims, session login, and SignalR isolation.
* [server/ControllerSecurityPlans.md](server/ControllerSecurityPlans.md) - controller authorization, permissions, and API safety.
* [server/FileStoragePlans.md](server/FileStoragePlans.md) - uploads, downloads, MinIO/S3 storage, path safety, file ownership, and git hygiene.
* [server/ModerationSecurityPlans.md](server/ModerationSecurityPlans.md) - group admin, global bans, IP blocking, rate limiting, login/device security, and query optimization.
* [server/OperationsReliabilityPlans.md](server/OperationsReliabilityPlans.md) - health checks, structured logs, audit logs, backup/restore, deployments, and error reporting.
* [shared/BugfixTriagePlans.md](shared/BugfixTriagePlans.md) - bug priority, reproduction notes, regression tests, and known current bugs.
* [shared/DeepLinksAndInvitesPlans.md](shared/DeepLinksAndInvitesPlans.md) - app links, group invites, call invites, profile links, and theme links.
* [shared/ProductRoadmapPlans.md](shared/ProductRoadmapPlans.md) - statuses, chat productivity, advanced search, company tools, AI assistance, bots, OAuth, and themes.
* [shared/TestingPlans.md](shared/TestingPlans.md) - test projects, server integration tests, database checks, storage contracts, and CI gates.
* [shared/RefactoringPlans.md](shared/RefactoringPlans.md) - large files, naming, namespaces, encoding, and verification gates.

## Recommended Implementation Order

1. Create bug repro notes and add test projects.
2. Add first regression tests for tenant context lifetime, permissions, file path safety, cache invalidation, and theme refresh.
3. Fix P0/P1 bugs: tenant `DbContext` lifetime, public endpoints, chat permissions, upload path safety, cache/display refresh, and broken install/shortcut behavior.
4. Add tenant identity to JWT and SignalR user routing.
5. Add group administration basics: roles, invite links, kick/ban, and audit log.
6. Add app deep links for group invites, call invites, profiles, and themes.
7. Move production files to MinIO/S3-compatible object storage behind `IFileStorageService`.
8. Add rate limiting, IP blocking, login/device security, and safer error handling.
9. Add health checks, structured logs, audit logs, backup/restore notes, and release safety.
10. Add database constraints and indexes.
11. Extract shared EF Core model configuration.
12. Split `AuthController`, `MainHub`, and the largest client files.
13. Add user statuses, pinned chats, saved messages, drafts, scheduled messages, auto-delete, polls, mentions, and advanced search.
14. Add company workspace tools: todos, mini Jira, calendar events, internal Markdown documentation, and group threads.
15. Add in-app updates, OAuth, Bots API, shared themes, and AI call notes.
16. Normalize file names, namespaces, and comments.

## First Testing Targets

Start with tests that protect the riskiest fixes:

* Tenant services should not dispose a context between multiple method calls in one operation.
* `PermissionService` should authorize base, admin, creator, and unrelated users correctly.
* Anonymous users should not access private user, message, chat member, or file endpoints.
* File download/delete should reject path traversal attempts.
* Tenant tokens and SignalR user identifiers should not cross workspaces.
* Cache invalidation should refresh chat/profile/message UI without reopening the chat.
* Theme switching should update visible controls without restarting the client.

## Backlog Categories

Use these categories when adding new work:

* `Bugfix` - broken existing behavior.
* `Hardening` - security, tenant isolation, permissions, validation, and rate limits.
* `Performance` - query optimization, caching, pagination, and profiling.
* `Client UX` - visual polish, dialogs, themes, sounds, previews, install/update flow.
* `Collaboration` - groups, invites, mentions, statuses, company calendar, todos, docs, tasks.
* `Platform` - MinIO/S3, OAuth, Bots API, in-app updates, deep links, shared themes.
* `Operations` - health checks, logs, audit logs, backups, restore, deployment, and support diagnostics.
* `AI` - transcription summaries, meeting notes, action items, and later chat summaries.

## Verification Notes

Last checked command:

```text
dotnet build uchat.sln --no-restore
```

Build passed with existing client package warnings for `Concentus` and `Microsoft.Web.WebView2`.
