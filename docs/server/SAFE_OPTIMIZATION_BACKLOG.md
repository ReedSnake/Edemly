# Safe Server Optimization Backlog

This document lists server-side work that is still safe to do after the current chat/message performance pass.

Safe means:

* no client route changes;
* no client workflow changes;
* no intentional response-contract break;
* no authorization behavior change without a separate explanation;
* no production database migration execution without an explicit migration plan.

## Current Baseline

The current server baseline includes the following performance and safety work:

* indexes for common chat/message access paths;
* paged message history ordering by `SentAt` and `Id`;
* chat cache invalidation from message hub methods;
* dedicated cache keys for message history and last-message lookup;
* chat last-message snapshot fields:
  * `LastMessageId`;
  * `LastMessageText`;
  * `LastMessageSenderId`;
  * existing `LastMessageTime`;
* migration backfill for last-message snapshot fields in `ServerDbContext` and `CompanyDbContext`;
* snapshot maintenance from message create, update, delete, call system messages, hub methods, and welcome messages;
* projected chat list and chat detail reads that avoid loading full entity graphs for normal client views;
* lighter projections for user, payment, note, permission, and chat-member read paths;
* batched welcome chat membership checks;
* chat update permission checks for group metadata and icons;
* message lookup access checks;
* chat-member lookup access checks;
* atomic message create plus chat snapshot update for HTTP and hub send paths;
* payment completion that marks the payment paid and upgrades the payment record's user in one transaction.

The migrations exist in source. Applying them to a real database requires a separate migration rollout plan.

## Safe Remaining Work

### 1. Add server tests for message history and chat snapshots

Add focused tests for:

* `GET /api/chats/{chatId}/messages` ordering and pagination;
* cache invalidation after create, update, and delete;
* `Chat.LastMessageId`, `LastMessageText`, `LastMessageSenderId`, and `LastMessageTime` after message create;
* snapshot update when the last message is edited;
* snapshot refresh when the last message is deleted;
* preservation of archived older messages in history.

This is the safest next step because it improves confidence without changing runtime behavior.

### 2. Reduce duplicate message write logic between `MainHub` and `MessageService`

`MainHub` still performs message create, update, delete, permission checks, cache invalidation, and snapshot maintenance directly.

A safe refactor would move duplicated mechanics into a shared application-level helper or service method while preserving:

* SignalR method names;
* SignalR event names;
* HTTP endpoints;
* permission behavior;
* response DTOs.

The hub should remain responsible for realtime broadcasting, while Application should own the message use case.

### 3. Add focused security and consistency tests

Add focused tests for:

* unauthenticated protected endpoints;
* users trying to read chats, messages, or chat members they cannot access;
* users trying to update chats without the required role;
* payment status checks for another user's order reference;
* payment return completing the payment record's user;
* failed icon update cleanup after an upload succeeds.

### 4. Batch chat-member creation where behavior is simple

Some chat creation paths add members one by one.

A safe optimization is to batch member inserts when:

* all users are already validated;
* roles are known;
* the operation uses one DbContext;
* the resulting members and permissions are identical.

This optimization should preserve existing chat-member authorization behavior.

### 5. Add cancellation tokens gradually

Chat and message service methods can accept and pass `CancellationToken` values through EF calls.

This is safe when done without changing public routes or response behavior. It is mostly useful for request cancellation and long-running queries.

### 6. Review remaining read indexes only with query evidence

Indexes are usually safe, but they still change the database schema and write cost.

More indexes should be tied to concrete query paths. New constraints that can reject existing data require a cleanup plan and data audit first.

### 7. Harden file and payment production boundaries

The current server behavior is safer than before, but production hardening still needs:

* real WayForPay provider verification;
* file ownership or attachment ACL rules if uploaded URLs should not be available to every authenticated user;
* stricter generic file validation if arbitrary file types are not intended;
* upload retention, quota, backup, and orphan cleanup rules.

## Not Safe For This Backlog

These tasks should be handled separately:

* changing API routes;
* changing SignalR event names;
* changing client chat logic;
* broad authorization redesign without focused tests;
* replacing the tenant database model;
* applying migrations to a real database without a rollout plan;
* large controller or hub rewrites without tests around the affected behavior.

## Related Documents

* [Server Architecture](ARCHITECTURE.md)
* [Database](DATABASE.md)
* [Realtime](REALTIME.md)
* [Security](SECURITY.md)
* [Testing](TESTING.md)
* [Shared Architecture Principles](../ARCHITECTURE_PRINCIPLES.md)
