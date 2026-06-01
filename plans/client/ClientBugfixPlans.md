# Edemly Client Bugfix Roadmap

## Phase 1. Cache and Display Refresh Bugs

### Goal

Fix stale UI state and incorrect cached data.

### Known Issues

* Some chat/profile/message data appears stale after updates.
* Some UI elements update only after reopening a chat.
* Cache invalidation is inconsistent between HTTP actions and SignalR events.

### Tasks

* Audit all client caches:
  * chat list cache
  * message cache
  * profile picture cache
  * file cache
  * contact/user cache
* Define cache ownership: what data is cached, when it expires, and which event invalidates it.
* Add explicit invalidation for profile updates, group updates, message updates, and file changes.
* Make SignalR events update local state immediately.
* Add tests for cache keys and invalidation rules where logic can be isolated.

### Result

Data changes appear without forcing the user to reopen chats or restart the client.

---

## Phase 2. Theme Refresh Fixes

### Goal

Make theme changes apply consistently across the whole client.

### Known Issues

Some elements do not change color until the chat is reopened or the app is restarted.

### Tasks

* Audit all UI controls that use hardcoded colors.
* Move colors into shared theme resources.
* Ensure dynamic resources are used where runtime theme switching is required.
* Refresh chat message controls, context menus, dialogs, calendar, notes, settings, and install/uninstall pages.
* Add a manual theme verification checklist until UI automation exists.

### Result

Theme changes apply immediately and consistently.

---

## Phase 3. Registration Names and Username Flow

### Goal

Stop creating confusing automatic profile data.

### Current Issue

Registration currently derives username/first name automatically from entered data.

### Tasks

* Decide registration fields:
  * email only
  * email plus display name
  * email plus username and display name
* Do not silently fill profile names if the user did not enter them.
* Validate username separately from display name.
* Add an onboarding/profile completion step if needed.
* Keep tenant and personal registration behavior consistent.

### Result

Usernames and names are intentional, clear, and editable.

---

## Phase 4. Notes Page Cleanup

### Goal

Make the notes page reliable and pleasant to use.

### Tasks

* Review current notes loading, editing, saving, deleting, and empty states.
* Fix stale note counts and stale note content after updates.
* Improve validation and error messages.
* Add search/filter if the page already has enough data to justify it.
* Ensure theme resources update correctly.

### Result

Notes feel like a stable part of the app instead of a rough side page.

---

## Phase 5. Install, Uninstall, and Shortcut Flow

### Goal

Fix the current installation page and make app removal/update behavior clear.

### Known Issues

* Install page is currently incorrect.
* Desktop shortcut is created incorrectly.
* Full install flow may not be needed right now.

### Tasks

* Replace the current install page with a cleaner app management page if installation is not needed.
* Fix desktop shortcut creation.
* Add uninstall/removal flow:
  * remove app files if applicable
  * remove cache
  * remove config if user chooses
  * remove shortcut
* Clearly separate "clear cache", "sign out", and "remove app" actions.
* Add safe confirmation dialogs.

### Result

Users can manage local app files without broken shortcut or install behavior.

---

## Phase 6. In-App Updates

### Goal

Allow the client to check for and install newer versions from inside the app.

### Tasks

* Decide update distribution:
  * download installer package
  * download portable build
  * use a release feed
* Add version check endpoint or release manifest.
* Display current version and latest version.
* Support update download, verification, and restart.
* Add rollback/manual download fallback.

### Result

Users can update Edemly without manually replacing files.

---

## Phase 7. Custom Message Windows

### Goal

Fix and standardize custom dialogs.

### Tasks

* Replace inconsistent message boxes with one app dialog system.
* Support info, warning, error, confirmation, and destructive confirmation states.
* Ensure dialogs follow the active theme.
* Ensure dialogs are keyboard-accessible.
* Use consistent wording for errors and confirmations.

### Result

All important prompts feel like part of the same application.

---

## Phase 8. Sounds and Call Feedback

### Goal

Improve call and notification sound quality.

### Tasks

* Replace current call sounds with polished assets.
* Normalize sound volume.
* Add settings for notification, message, and call sound toggles.
* Respect Do Not Disturb status once user statuses exist.
* Test repeated calls and sound stop behavior.

### Result

Call and notification audio feels intentional and does not get stuck.

---

## Phase 9. Multi-File Attachment Preview

### Goal

Improve the experience of sending multiple files.

### Current Issue

Files are sent one by one without a good grouped preview.

### Tasks

* Add a composer preview for multiple selected files.
* Group files into one send action where possible.
* Show thumbnails for images and file-type icons for documents.
* Allow removing individual files before sending.
* Keep upload progress visible per file and for the group.
* Represent grouped attachments in message rendering.

### Result

Sending multiple files feels organized and predictable.

