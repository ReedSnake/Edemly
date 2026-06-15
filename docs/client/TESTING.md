# Client Testing

This document describes the client testing approach and the most useful coverage areas.

The WPF client has many UI-heavy workflows. Tests should focus first on behavior that can be validated without fragile visual automation, then add UI automation only where it protects a critical workflow.

## Contents

* [Overview](#overview)
* [Test Project](#test-project)
* [Running Tests](#running-tests)
* [Current Coverage](#current-coverage)
* [Recommended Coverage](#recommended-coverage)
* [Build Verification](#build-verification)
* [Manual Checks](#manual-checks)
* [Current Limitations](#current-limitations)
* [Related Documents](#related-documents)

## Overview

Client tests live in:

```text
Edemly.Client.Tests
```

Prefer tests around non-visual logic first:

* startup configuration resolution;
* API URL and hub URL selection;
* cache behavior;
* attachment descriptors and file icon resolution;
* auth input validation;
* profile input validation;
* chat-list state creation;
* call session state transitions;
* localization fallback behavior.

UI code should be kept thin enough that most behavior can be tested through Application, Infrastructure, or presentation helper classes.

## Test Project

The current test project is:

```text
Edemly.Client.Tests/Edemly.Client.Tests.csproj
```

It is an NUnit project. The current checked-in test file is still a minimal skeleton.

## Running Tests

From the repository root:

```powershell
dotnet test Edemly.Client.Tests\Edemly.Client.Tests.csproj
```

For full solution validation:

```powershell
dotnet build Edemly.sln
```

For client-only compile validation:

```powershell
dotnet build Edemly.Client\Edemly.Client.csproj
```

## Current Coverage

Current client automated coverage is minimal.

The test project exists and contains a placeholder NUnit test. Treat it as infrastructure, not meaningful client behavior coverage.

## Recommended Coverage

High-value test targets:

| Area | Examples |
| ---- | -------- |
| startup | command-line server URL, static `client.json`, saved config fallback, hub URL override |
| auth | email/code validation, session token restore behavior, logout cleanup |
| cache | chat/message/user expiry, media cache key generation, cache scope separation |
| API | URL normalization, bearer token header updates, response fallback behavior |
| realtime | payload parsing, connection-state transitions, event dispatch behavior |
| chat UI helpers | `ChatListItemStateFactory`, direct chat display names, unread/status state |
| attachments | descriptor creation, file kind detection, clipboard temp file behavior |
| calls | `CallSessionState` and `CallSessionController` phase transitions |
| theming | theme name validation and resource application behavior where practical |

Tests should not depend on a live server unless they are explicitly integration or smoke tests.

## Build Verification

Client refactors should pass at least:

```powershell
dotnet build Edemly.Client\Edemly.Client.csproj
```

Cross-project changes should pass:

```powershell
dotnet build Edemly.sln
```

Common WPF compile issue: the `Edemly.Client.Application` namespace can shadow `System.Windows.Application`. Use `System.Windows.Application.Current` in client code when the reference is ambiguous.

## Manual Checks

Manual checks are still useful for WPF behavior that is expensive to automate:

* launch from static `client.json`;
* login/session restore;
* chat list loading and direct-chat display names;
* message send/edit/delete;
* profile picture update and cache refresh;
* group icon update;
* attachment upload/download/open;
* optional and mandatory update banners;
* theme switching;
* language switching;
* direct and group call lifecycle.

Manual checks should be short and scenario-based. They should not replace automated tests for pure logic.

## Current Limitations

* Automated client behavior coverage is still very small.
* Most WPF UI is not covered by UI automation.
* Realtime and call behavior need focused tests around state transitions before large refactors.
* Some helpers are still coupled to WPF types, which makes them harder to unit test.

## Related Documents

* [Client Architecture](ARCHITECTURE.md)
* [API Clients](API_CLIENTS.md)
* [Realtime Communication](REALTIME.md)
* [Caching](CACHING.md)
* [Theming](THEMING.md)
