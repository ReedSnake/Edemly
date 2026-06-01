# Edemly Bugfix Triage Plan

## Purpose

Keep bugs, refactors, and new features separate so urgent fixes do not disappear inside long-term roadmap work.

## Bug Categories

```text
P0 - data loss, security bypass, app cannot start
P1 - core workflow broken
P2 - visible UX bug with workaround
P3 - polish, wording, minor visual issue
```

## Phase 1. Create Repro Notes

### Goal

Every important bug should have a short reproduction path before implementation starts.

### Tasks

* Record affected area.
* Record steps to reproduce.
* Record expected behavior.
* Record actual behavior.
* Record whether it is tenant-specific or personal-mode-specific.
* Add screenshots or short notes where UI state matters.

### Result

Bugs become testable instead of vague.

---

## Phase 2. Convert Bugs Into Regression Tests

### Goal

Prevent fixed bugs from coming back.

### Tasks

* Add a failing test before fixing server-side logic when practical.
* Add client unit tests for isolated logic such as cache invalidation and URL building.
* Add manual verification checklists for WPF-only visual bugs.
* Link bug notes to test names.

### Result

Each important bug fix leaves behind a guardrail.

---

## Phase 3. Current Known Bugs

### Client

* Cache/display state becomes stale.
* Theme changes do not update all elements immediately.
* Registration fills username/name data in confusing ways.
* Notes page needs cleanup.
* Install page is incorrect.
* Desktop shortcut creation is incorrect.
* Custom message windows need fixes.
* Call sounds need replacement or cleanup.
* Multiple file sending lacks a grouped preview.

### Server

* Tenant `DbContext` lifetime can break multi-step operations.
* Some endpoints expose data without authorization.
* Chat permission checks need corrections.
* Upload authorization and file path handling need hardening.
* Query performance needs profiling and optimization.

### Shared

* Deep links are not defined yet.
* Group/call invite flows are not implemented.
* User status behavior is not implemented.

### Result

Known problems are visible and can be prioritized before feature work.

