# Edemly Database Roadmap

## Phase 1. Stabilize Tenant Schemas

### Goal

Make master and company database schemas predictable and consistent.

### Tasks

* Extract shared EF Core model configuration from `ServerDbContext` and `CompanyDbContext`.
* Move common entity mappings into `IEntityTypeConfiguration` classes or shared `ModelBuilder` extensions.
* Keep master-only entities, such as `Company`, isolated from tenant schema configuration.
* Recreate migrations after shared mappings are cleaned up.
* Verify table names are identical where the same entity exists in both schemas.

### Result

Master and tenant databases evolve together without accidental schema drift.

---

## Phase 2. Fix Email Allowlist Table

### Goal

Make tenant email allowlists reliable and queryable.

### Current Issue

Tenant migrations create `Emails`, while master maps `Email` to `email`.

```text
Tenant table:
Emails
------
Id
EmailAddress longtext
```

### Tasks

* Choose one table name, preferably `email_allowlist` or `allowed_email`.
* Change `EmailAddress` from `longtext` to `varchar(255)`.
* Normalize emails to lower-case before saving and comparing.
* Add a unique index on normalized email.
* Decide whether master needs this table at all; if not, remove `DbSet<Email>` from `ServerDbContext`.

### Result

Allowed company emails are unique, indexed, and consistent across environments.

---

## Phase 3. Protect Chat Membership Integrity

### Goal

Prevent duplicate chat memberships and inconsistent private chats.

### Tasks

* Add a unique index on `(chat_id, user_id)` in `chat_member`.
* Update `ChatMemberService.AddMember` to rely on the unique constraint and handle duplicate insert races.
* Add tests for adding the same user to the same chat twice.
* Consider a normalized direct-chat identity for 1-on-1 chats.
* Prevent duplicate direct chats for the same pair of users under parallel requests.

### Result

Each user can belong to a chat only once, and private chat creation becomes race-safe.

---

## Phase 4. Improve Message and Chat Query Indexes

### Goal

Make chat loading and message pagination efficient as data grows.

### Tasks

* Add a composite index on `message(chat_id, sent_at)`.
* Keep existing indexes on `message.chat_id` and `message.sender_id` only if the query plan still benefits from them.
* Add an index for chat sorting if `last_message_time` becomes the main list order.
* Ensure `ChatService.GetMyChats` avoids one query per chat for the last message.
* Prefer projecting last message data in one query or keeping `Chat.LastMessageTime` and preview fields updated.

### Result

Chat lists and message history remain fast with many messages.

---

## Phase 5. Harden Sessions and Payments

### Goal

Make session and payment records safe for lookup and callbacks.

### Tasks

* Add a unique index on `session_info.session_token`.
* Keep or revisit the unique index on `session_info.user_id`, depending on whether multiple devices should be allowed.
* Add a unique index on `payment.transaction_id`.
* Add an index on `payment(status, updated_at)` for the background worker.
* Make payment callback processing idempotent.

### Result

Session lookup and payment updates are deterministic and race-safe.

---

## Phase 6. Review Delete Behavior

### Goal

Avoid accidental data loss from cascading deletes.

### Tasks

* Review cascade delete from `user` to messages, payments, sessions, reminders, chat members, and calls.
* Decide which data should be anonymized instead of deleted.
* Keep notes with `Restrict` behavior or add explicit delete workflows.
* Document the expected behavior for account deletion.

### Result

Deleting a user cannot unexpectedly erase important business history.

