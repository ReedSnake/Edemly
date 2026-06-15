# Server Testing

This document describes the server testing approach and the highest-value coverage areas.

## Contents

* [Overview](#overview)
* [Test Project](#test-project)
* [Test Database](#test-database)
* [Running Tests](#running-tests)
* [Coverage Priorities](#coverage-priorities)
* [Message And Chat Tests](#message-and-chat-tests)
* [Security Tests](#security-tests)
* [File Storage Tests](#file-storage-tests)
* [Payment Tests](#payment-tests)
* [Deployment Smoke Checks](#deployment-smoke-checks)
* [Current Gaps](#current-gaps)
* [Related Documents](#related-documents)

## Overview

Server tests should protect behavior that is hard to verify manually:

* authorization and ownership checks;
* chat membership boundaries;
* message history ordering and cache invalidation;
* chat last-message snapshot updates;
* tenant-aware database resolution;
* payment ownership and subscription updates;
* file access rules;
* transaction boundaries where partial persistence would be visible.

Performance-only changes should still have behavior tests when they touch shared query paths or cached data.

## Test Project

Server tests live in:

```text
Edemly.Server.Tests/
```

Use integration tests for controller/service behavior that depends on EF Core, authentication, tenant resolution, caching, or transactions.

Use smaller service tests when the behavior can be exercised without HTTP routing or SignalR.

## Test Database

The server test infrastructure uses SQLite in-memory databases for integration tests.

This keeps tests isolated from local MySQL and avoids mutating developer or production data. MySQL-specific behavior should still be validated separately when a change depends on provider-specific SQL, index behavior, or migration behavior.

Tenant-aware tests should create both global and tenant contexts when verifying tenant isolation.

## Running Tests

From the repository root:

```powershell
dotnet test Edemly.Server.Tests\Edemly.Server.Tests.csproj
```

For broad validation before committing server changes:

```powershell
dotnet build Edemly.sln
```

Server changes should at minimum pass `dotnet build Edemly.sln` before they are committed.

## Coverage Priorities

Prioritize tests in this order:

1. Authorization bugs that could expose another user's data.
2. Partial-save bugs where one table is updated and another table is not.
3. Cache invalidation bugs that show stale data to the client.
4. Query optimizations that change projection shape or sorting.
5. Tenant isolation and file access boundaries.

## Message And Chat Tests

Add focused coverage for:

* `GET api/chats/{chatId}/messages` requires chat membership;
* message history is ordered by `SentAt` and `Id`;
* pagination keeps stable ordering across pages;
* message history returns archived older messages instead of only active/recent rows;
* message create invalidates message history and last-message cache keys;
* message update invalidates affected cache keys;
* message delete invalidates affected cache keys;
* `Chat.LastMessageId`, `LastMessageText`, `LastMessageSenderId`, and `LastMessageTime` are set after message create;
* editing the current last message updates the snapshot;
* deleting the current last message refreshes the snapshot to the previous message;
* deleting the only message clears the snapshot;
* `ChatService.GetMyChatsAsync` projection still returns the same client-facing `ChatDto` values and sorting;
* direct chat display names still use the other participant.

Hub message tests should verify the same core behavior as HTTP message tests while preserving SignalR method and event names.

## Security Tests

Add tests for:

* unauthenticated protected endpoints return unauthorized;
* chat detail rejects users who are not members;
* chat update rejects non-members and members without the required role;
* chat icon upload rejects users who cannot update the chat;
* chat-member lookup by id rejects users outside the member's chat;
* message lookup rejects users outside the message's chat;
* note operations stay scoped to the current user as creator;
* reminding operations stay scoped to the current user;
* company create and allowed-email management require Admin;
* public endpoints are intentionally public and documented.

These tests should assert response status and absence of side effects when access is denied.

## File Storage Tests

Add tests for:

* profile picture upload accepts only allowed image extensions;
* chat icon upload validates image extensions and chat permissions;
* generic file upload enforces the configured size limit;
* `api/files/download` requires authentication;
* `/uploads/...` requires authentication through middleware/controller paths;
* global context cannot read tenant-prefixed files;
* delete fails for files outside the accessible tenant scope;
* failed chat icon update attempts best-effort cleanup of the uploaded file.

MinIO-backed tests should be separated from normal integration tests unless they can run against a lightweight local container reliably.

## Payment Tests

Add tests for:

* payment initiation rejects invalid amounts;
* payment history returns only the current user's payments;
* payment status check rejects order references that do not belong to the current user;
* payment return completes the payment record's user, not a user id supplied by the caller;
* paid completion updates payment status and user subscription in one transaction;
* failed provider status marks the payment failed without upgrading the user;
* WayForPay test mode and real verification mode are clearly separated.

The current provider status check is stub-like and should be replaced or isolated before production payment tests can be considered complete.

## Deployment Smoke Checks

For local environment validation, use smoke checks rather than full integration tests:

```powershell
docker compose -f deployment/local/docker-compose.yml config
docker compose -f deployment/local/docker-compose.yml up --build
```

Then verify:

* `http://localhost:3500/health`
* `http://localhost:3500/gateway/health`
* `http://localhost:8080/client.json`
* `http://localhost:3700` is reachable through the hub gateway when the client connects

These checks require Docker and are not part of the normal server test suite.

## Current Gaps

* Message history, cache invalidation, and snapshot tests are still the highest-priority missing tests.
* Payment provider verification is not production-grade while `CheckPaymentStatusAsync` remains stub-like.
* File ownership/ACL tests need a clearer file ownership model.
* Multi-backend behavior cannot be fully tested until Redis/backplane work exists.
* Some existing behavior may be covered by integration tests, but coverage should not be treated as complete without a fresh test audit.

## Related Documents

* [Server Architecture](ARCHITECTURE.md)
* [Server API](API.md)
* [Server Security](SECURITY.md)
* [Server Realtime](REALTIME.md)
* [Server Database](DATABASE.md)
* [Safe Optimization Backlog](SAFE_OPTIMIZATION_BACKLOG.md)
