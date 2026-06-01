# Edemly Product Roadmap

## Phase 1. Chat Quality of Life

### Goal

Improve daily messaging ergonomics.

### Features

* Pin chats.
* Saved messages.
* Drafts for unsent messages.
* Scheduled messages.
* Auto-delete messages.
* Polls.
* Mentions in groups.
* Search by phone number.

### Tasks

* Define data models for pinned chats, saved messages, drafts, scheduled messages, and polls.
* Add mention parsing and notification rules.
* Decide whether drafts are local-only or synced.
* Decide message auto-delete retention behavior.
* Add tests for message lifecycle and notification behavior.

### Result

Chats become more useful for everyday work and personal communication.

---

## Phase 2. User Presence and Status

### Goal

Make availability visible and controllable.

### Statuses

```text
Online
Away
Do Not Disturb
Invisible
Custom Status
Last Active
```

### Tasks

* Add status model and API.
* Sync status through SignalR.
* Add client UI for selecting status.
* Respect Do Not Disturb for sounds and notifications.
* Show last active time where privacy rules allow.
* Decide how Invisible affects presence events.

### Result

Users can communicate availability without leaving chats.

---

## Phase 3. Advanced Search

### Goal

Find messages and attachments quickly.

### Search Areas

* Messages.
* Files.
* Attachments.
* Users.
* Chats.
* Company documents.

### Tasks

* Add server-side search endpoints with pagination.
* Add filters by chat, sender, date, attachment type, and company.
* Add attachment metadata indexing.
* Add UI for advanced search.
* Consider full-text search when database search becomes insufficient.

### Result

Users can find old work without scrolling through chats.

---

## Phase 4. Company Productivity Tools

### Goal

Turn company workspaces into lightweight collaboration spaces.

### Features

* Company todo lists.
* Mini Jira-style tasks.
* Company calendar events.
* Events assigned to other members.
* Calls/events created from calendar.
* Internal company documentation.
* Documentation branches or sections.
* Group chat threads.

### Tasks

* Define task, todo, event, and document models.
* Add company-level permissions.
* Allow creating events for other participants.
* Link calendar events to chats or calls.
* Render Markdown documents in the client.
* Add small navigation tree for documentation.
* Add threads for group chats without breaking message history.

### Result

Company workspaces support planning, documentation, and task tracking.

---

## Phase 5. AI Assistance

### Goal

Use AI to reduce meeting and planning friction.

### Features

* Analyze call transcripts.
* Generate meeting notes.
* Extract action items.
* Assign suggested tasks to participants.
* Summarize long chats later.

### Tasks

* Implement call transcription first.
* Store transcript text and speaker metadata.
* Add AI summary generation as an optional action.
* Require permissions before analyzing private/company content.
* Show generated notes as editable drafts, not final truth.
* Track cost and rate limits.

### Result

Calls and discussions can produce useful follow-up notes.

---

## Phase 6. Extensibility and Integrations

### Goal

Prepare Edemly for external integrations.

### Features

* Bots API.
* OAuth login.
* OAuth app authorization.
* Webhooks later.
* Shared user themes.

### Tasks

* Design bot identity and permissions.
* Decide OAuth providers.
* Add OAuth account linking.
* Add token storage and revocation.
* Add rate limits for bots.
* Add theme import/export and link install flow.

### Result

Edemly can grow beyond a closed client-server app.

