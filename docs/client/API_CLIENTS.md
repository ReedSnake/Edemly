# Client API Clients

This document describes how the WPF client communicates with the server over HTTP.

The API layer should stay focused on transport: routes, requests, responses, serialization, and authentication headers. User workflows belong in Application or Presentation code.

## Contents

* [Overview](#overview)
* [Main Components](#main-components)
* [Client Registry](#client-registry)
* [Authentication](#authentication)
* [Endpoint Clients](#endpoint-clients)
* [Files and Payments](#files-and-payments)
* [Error Handling](#error-handling)
* [Current Limitations](#current-limitations)
* [Related Documents](#related-documents)

## Overview

The client API layer lives under:

```text
Edemly.Client/Api
```

It uses shared DTOs from `Edemly.Contracts` and a shared `HttpClient` configured through `ApiClientContext`.

The client currently has endpoint-specific API clients for:

* authentication;
* users;
* chats;
* chat members;
* messages;
* files;
* notes;
* payments;
* remindings.

## Main Components

| Component | Responsibility |
| --------- | -------------- |
| `ApiClientContext` | Owns the shared `HttpClient`, base URL, and bearer token header |
| `ApiClientBase` | Provides common HTTP helpers and JSON deserialization |
| `IApiClients` | Aggregates endpoint clients behind one access point |
| `ApiClients` | Creates endpoint client instances from one `ApiClientContext` |
| Endpoint clients | Own server route calls for one domain area |

The shared `HttpClient` base address is the selected API base URL. Tenant/company context is applied before the registry initializes the API clients.

## Client Registry

`ClientServiceRegistry` creates the API clients during startup:

```text
ClientServiceRegistry.Initialize(apiBase, hubBase, cacheScope)
```

Initialization creates:

* one shared `HttpClient`;
* one `ApiClientContext`;
* one `ApiClients` aggregate;
* an `AuthService`;
* a `HubService`;
* media caches scoped to the selected company or personal workspace.

The registry also updates the bearer token through:

```text
ClientServiceRegistry.SetAuthToken(token)
```

This updates the API context and media caches that need authenticated upload/download requests.

## Authentication

Protected server endpoints use JWT Bearer authentication.

The current token flow is:

1. `AuthService` calls the auth API.
2. The returned access token is stored by `SecureStorageService`.
3. `ClientServiceRegistry.SetAuthToken` sets the `Authorization: Bearer` header on the shared HTTP context.
4. Realtime connections receive the same access token when `HubService.ConnectAsync` is called.

Session and local configuration values are persisted through `ConfigService` and `SecureStorageService`.

## Endpoint Clients

Endpoint clients are intentionally small.

| Client | Area |
| ------ | ---- |
| `AuthApiClient` | login, registration, verification, session login, logout |
| `UserApiClient` | current user, public user lookup, search, batch load, profile update |
| `ChatApiClient` | private chat, group chat, chat list, chat detail, chat update |
| `ChatMembersApiClient` | member lookup and member management |
| `MessageApiClient` | message history and message lookup |
| `FileApiClient` | file upload, profile picture upload, chat icon upload, download |
| `NoteApiClient` | contact notes and note count |
| `PaymentApiClient` | payment initiation, history, status |
| `RemindingApiClient` | reminders and completion changes |

Application services and presentation controllers should call these clients instead of constructing raw HTTP requests.

## Files and Payments

File and payment flows have a few special rules:

* file upload uses multipart form-data;
* file downloads may use direct `/uploads/...` URLs or the server download endpoint;
* file and profile-picture caches add bearer tokens for protected upload paths;
* payment initiation returns server-generated HTML for the payment provider flow;
* payment status and history remain scoped to the current authenticated user.

The client should continue to treat server routes and DTOs as contracts. Route changes require coordinated server and client changes.

## Error Handling

`ApiClientBase` catches transport and JSON errors, writes debug output, and returns `default`, `false`, or `(false, error)` depending on the helper.

This keeps UI flows from crashing on common network failures, but it also means callers must handle empty results explicitly.

For user-facing workflows, Application or Presentation code should convert failed API results into clear UI states or messages.

## Current Limitations

* API clients return simple success/null values rather than a unified typed result model.
* Some endpoint clients still contain endpoint-specific parsing or response-shape handling.
* `AuthService` still receives the API base URL directly instead of being fully composed from the shared API context.
* Debug logging is useful for local diagnosis but is not a full telemetry model.

## Related Documents

* [Client Architecture](ARCHITECTURE.md)
* [Realtime Communication](REALTIME.md)
* [Caching](CACHING.md)
* [Testing](TESTING.md)
* [Server API](../server/API.md)
