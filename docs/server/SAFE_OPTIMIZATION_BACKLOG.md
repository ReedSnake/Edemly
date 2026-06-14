# Safe Server Optimization Backlog

This document lists server-side work that is still safe to do after the current chat/message performance pass.

Safe means:

* no client route changes;
* no client workflow changes;
* no intentional response-contract break;
* no authorization behavior change without a separate explanation;
* no production database migration execution without an explicit migration plan.

## Current Baseline

The recent message performance pass added:

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
* snapshot maintenance from message create, update, delete, call system messages, hub methods, and welcome messages.

The migrations were created but not applied to a database in this pass.

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

### 2. Project chat list data instead of loading full entities

`ChatService.GetMyChatsAsync` can be optimized further by projecting only the fields needed for `ChatDto` and direct-chat display names.

Current behavior should stay the same:

* route stays `GET /api/chats`;
* `ChatDto` stays compatible;
* direct chats still show the other participant name;
* sorting still uses `LastMessageTime ?? CreatedAt`.

The goal is to reduce EF materialization and avoid loading more member/user data than the chat list needs.

### 3. Reduce duplicate message write logic between `MainHub` and `MessageService`

`MainHub` still performs message create, update, delete, permission checks, cache invalidation, and snapshot maintenance directly.

A safe refactor would move duplicated mechanics into a shared application-level helper or service method while preserving:

* SignalR method names;
* SignalR event names;
* HTTP endpoints;
* permission behavior;
* response DTOs.

The hub should remain responsible for realtime broadcasting, while Application should own the message use case.

### 4. Wrap message create plus snapshot update in a transaction

Message creation currently needs the message ID before the chat snapshot can be written.

A safe improvement is to keep the same external behavior but ensure the message insert and chat snapshot update are committed atomically. This avoids a partial state if message creation succeeds but snapshot update fails.

Do this for both HTTP message creation and hub message creation.

### 5. Batch chat-member creation where behavior is simple

Some chat creation paths add members one by one.

A safe optimization is to batch member inserts when:

* all users are already validated;
* roles are known;
* the operation uses one DbContext;
* the resulting members and permissions are identical.

Do not change chat-member authorization behavior as part of this optimization.

### 6. Add cancellation tokens gradually

Chat and message service methods can accept and pass `CancellationToken` values through EF calls.

This is safe when done without changing public routes or response behavior. It is mostly useful for request cancellation and long-running queries.

### 7. Review remaining read indexes only with query evidence

Indexes are usually safe, but they still change the database schema and write cost.

Only add more indexes when there is a concrete query path and the index is non-unique unless data has been audited first. Do not add new constraints that can reject existing data without a cleanup plan.

## Not Safe For This Backlog

These tasks should be handled separately:

* changing API routes;
* changing SignalR event names;
* changing client chat logic;
* changing authorization rules;
* replacing the tenant database model;
* applying migrations to a real database without a plan;
* large controller or hub rewrites without tests around the affected behavior.

## Suggested Next Chat Scope

Recommended next task:

```text
Continue on branch perf/message-history-optimization.

Use docs/server/SAFE_OPTIMIZATION_BACKLOG.md as the current safe backlog.
Do not change client behavior or API routes.
Start with server tests for message history, cache invalidation, and chat last-message snapshot behavior.
Then optimize ChatService.GetMyChatsAsync projection if tests/build are green.
Run dotnet build Edemly.sln before committing.
```

## Related Documents

* [Server Architecture](ARCHITECTURE.md)
* [Database](DATABASE.md)
* [Realtime](REALTIME.md)
* [Shared Architecture Principles](../ARCHITECTURE_PRINCIPLES.md)
