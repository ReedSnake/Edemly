# Client Caching

This document describes local caching and persisted client state.

Client caching is used to reduce duplicate network calls, keep media loading smooth, and preserve local preferences between launches. It is not a replacement for server-side authorization or source-of-truth data.

## Contents

* [Overview](#overview)
* [Cache Types](#cache-types)
* [Chat Cache](#chat-cache)
* [Media Caches](#media-caches)
* [Configuration Storage](#configuration-storage)
* [Secure Token Storage](#secure-token-storage)
* [Cache Scope](#cache-scope)
* [Invalidation](#invalidation)
* [Current Limitations](#current-limitations)
* [Related Documents](#related-documents)

## Overview

Caching code lives mainly under:

```text
Edemly.Client/Infrastructure/Caching
Edemly.Client/Infrastructure/Storage
```

`ClientServiceRegistry` owns the main cache instances:

* `ChatCache`
* `ProfilePictureCache`
* `FileCache`

The registry can clear conversation state, media caches, or all caches depending on the workflow.

## Cache Types

| Cache | Storage | Purpose |
| ----- | ------- | ------- |
| `ChatCache` | memory | chats, messages, users |
| `ProfilePictureCache` | disk plus memory image loading | profile pictures |
| `FileCache` | disk | generic file attachments |
| `ConfigService` | `%AppData%/Edemly/config.json` | user preferences and selected endpoints |
| `SecureStorageService` | `%AppData%/Edemly/.token` | encrypted JWT access token |

## Chat Cache

`ChatCache` stores frequently used DTOs in memory.

Current cache lifetimes:

| Data | Lifetime |
| ---- | -------- |
| chats | 30 minutes |
| messages | 10 minutes |
| users | 15 minutes |

The cache uses reader/writer locks and stores:

* chat DTOs by chat id;
* message lists by chat id;
* user DTOs by user id.

Message lists are sorted by `SentAt` when added. Realtime message updates can add, update, remove, or invalidate cached messages for a chat.

## Media Caches

`ProfilePictureCache` and `FileCache` download media through authenticated HTTP requests when needed.

Shared behavior:

* receives the server base URL during startup;
* receives the current bearer token or token provider;
* scopes disk folders by selected company or personal workspace;
* de-duplicates concurrent downloads for the same URL;
* retries failed downloads;
* exposes download started/completed/failed events.

`ProfilePictureCache` returns WPF `BitmapImage` instances.

`FileCache` returns local disk paths for downloaded attachments and can fall back to the server download endpoint when a direct URL fails.

## Configuration Storage

`ConfigService` stores normal user preferences and environment choices in:

```text
%AppData%/Edemly/config.json
```

Current values include:

* language;
* theme;
* selected company;
* install state;
* API server URL;
* hub server URL;
* static client config URL;
* update feed URL;
* background image path.

Configuration values are not encrypted, so access tokens and other secrets belong in secure storage instead.

## Secure Token Storage

`SecureStorageService` stores the access token in:

```text
%AppData%/Edemly/.token
```

The token is encrypted with Windows DPAPI using `DataProtectionScope.CurrentUser`. Only the current Windows user can decrypt it.

If token loading fails, the token file is cleared.

## Cache Scope

The app uses a cache scope to separate personal and company contexts.

Startup resolves:

```text
cacheScope = "personal"
```

or:

```text
cacheScope = {company name}
```

`ProfilePictureCache` and `FileCache` use that scope in their local folder names so media from one company context does not mix with another.

## Invalidation

Common invalidation paths:

| Trigger | Behavior |
| ------- | -------- |
| logout | clears auth token and conversation state |
| company switch | reinitializes services and uses a new cache scope |
| profile picture update | refreshes affected profile picture URL |
| file upload/update | can force media refresh for the changed URL |
| realtime message update/delete | updates or invalidates cached messages |

The caches are client-side performance helpers. If stale data is possible, the workflow should prefer a server refresh.

## Current Limitations

* `ChatCache` is in memory and is lost when the process exits.
* Media cache retention and maximum disk size are not centrally documented in code.
* Config storage is plain JSON and should not contain secrets.
* Cache invalidation depends on each workflow calling the right cache method.
* The client does not have a single cache policy abstraction across chat, profile pictures, files, and notes.

## Related Documents

* [Client Architecture](ARCHITECTURE.md)
* [API Clients](API_CLIENTS.md)
* [Realtime Communication](REALTIME.md)
* [Testing](TESTING.md)
* [Server File Storage](../server/FILE_STORAGE.md)
