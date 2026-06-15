# Client Realtime

This document describes how the WPF client uses SignalR for chat, presence, reminders, group updates, and calls.

SignalR method and event names are part of the server/client contract. Keep them aligned with the server realtime documentation and shared contracts.

## Contents

* [Overview](#overview)
* [Main Components](#main-components)
* [Connection Lifecycle](#connection-lifecycle)
* [Main Hub Events](#main-hub-events)
* [Call Hub Events](#call-hub-events)
* [Chat Integration](#chat-integration)
* [Call Integration](#call-integration)
* [Connection Status UI](#connection-status-ui)
* [Current Limitations](#current-limitations)
* [Related Documents](#related-documents)

## Overview

Realtime code lives under:

```text
Edemly.Client/Infrastructure/Realtime
```

`HubService` owns SignalR connections and exposes strongly named C# events to the rest of the client.

The client uses two server hubs:

| Server route | Client connection | Purpose |
| ------------ | ----------------- | ------- |
| `/main` | main connection | chat messages, group/profile updates, presence, reminders |
| `/call` | call connection | call lifecycle, signaling, mute state, audio chunks |

The main connection starts during authenticated app startup. The call connection is started on demand when call operations need it.

## Main Components

| Component | Responsibility |
| --------- | -------------- |
| `IHubService` | Realtime contract used by the client |
| `HubService` | Main SignalR implementation |
| `HubService.Connection` | connect, disconnect, retry, and connection state handling |
| `HubService.MainHandlers` | main hub event handlers |
| `HubService.Messages` | send/update/delete message hub calls |
| `HubService.CallHandlers` | call hub event handlers |
| `HubService.Calls` | call hub method calls |
| `HubConnectionFactory` | creates configured SignalR connections |
| `HubConstants` | method names and reconnect timing |
| `HubPayloadParser` | parses loosely shaped realtime payloads |
| `AppRealtimeCoordinator` | connects hub state to app-level UI and incoming-call handling |

## Connection Lifecycle

The connection lifecycle is:

1. Startup resolves the API and hub base URLs.
2. The user authenticates or restores a session.
3. The app passes the access token to `HubService.ConnectAsync`.
4. `HubService` creates the `/main` connection and registers handlers.
5. Connection state changes update `ConnectionStatusBar`.
6. Call operations start the `/call` connection when needed.
7. Logout or shutdown calls `DisconnectAsync`.

The SignalR access token is passed through the connection factory. The server reads it as a bearer token for hub authentication.

## Main Hub Events

The main hub handles normal messaging and app updates.

Client events exposed by `IHubService`:

| Event | Source event | Purpose |
| ----- | ------------ | ------- |
| `MessageReceived` | `ReceiveMessage` | A new chat message arrived |
| `MessageUpdated` | `ReceiveMessageUpdate` | A message was edited or a call system message changed |
| `MessageDeleted` | `ReceiveMessageDelete` | A message was deleted |
| `GroupCreated` | `GroupCreated` | A group chat was created |
| `GroupUpdated` | `GroupUpdated` | Group metadata changed |
| `ProfileUpdated` | `ProfileUpdated` | A user's profile picture changed |
| `UserStatusChanged` | `UserStatusChanged` | Presence changed |

Client methods sent to the main hub:

| Method | Purpose |
| ------ | ------- |
| `SendMessage` | sends a chat message |
| `UpdateMessage` | edits a chat message |
| `DeleteMessage` | deletes a chat message |
| `NotifyProfileUpdated` | informs other clients about a profile picture change |
| `NotifyGroupUpdated` | informs other clients about group metadata changes |
| `ConfirmRemindingReceived` | confirms reminder delivery |
| `GetUserStatus` | queries the current online status for a user |

## Call Hub Events

The call hub handles call lifecycle and signaling.

Client events exposed by `IHubService`:

| Event | Source event | Purpose |
| ----- | ------------ | ------- |
| `IncomingCallReceived` | `IncomingCall` | A direct incoming call |
| `CallingReceived` | `Calling` | Outgoing/group call notification |
| `CallAcceptedReceived` | `CallAccepted` | A participant accepted |
| `CallAcceptedDetailsReceived` | `CallAccepted` | Accepted-call payload with details |
| `CallRejectedReceived` | `CallRejected` | A participant rejected |
| `CallEndedReceived` | `CallEnded` | A call ended |
| `CallParticipantUpdatedReceived` | `CallParticipantUpdated` | Participant state changed |
| `GroupCallUpdated` | `GroupCallUpdated` | Group call state changed |
| `AudioChunkReceived` | `AudioChunk` | Audio data received from another participant |

Client methods sent to the call hub:

| Method | Purpose |
| ------ | ------- |
| `StartCall` | starts a direct or group call |
| `AcceptCall` | accepts a call |
| `RejectCall` | rejects a call |
| `EndCall` | ends or leaves a call |
| `SetCallMuted` | changes mute state |
| `SendAudioChunk` | sends encoded audio data |

## Chat Integration

Chat screen realtime behavior is coordinated by:

```text
Presentation/Controllers/Chats/ChatWorkspaceController.*
```

The controller subscribes to hub events and updates UI-facing chat state:

* message add/update/delete;
* group creation and group updates;
* profile picture updates;
* presence cache and chat header status;
* current chat mutations.

Chat list item state is built through `ChatListItemStateFactory` and rendered through `ChatListItemBuilder`.

## Call Integration

Call state is coordinated by:

```text
Application/Calls/CallSessionState.cs
Application/Calls/CallSessionController.cs
Application/Calls/CallWindowCoordinator.cs
Presentation/Windows/Calls/CallWindow.xaml.cs
```

`CallSessionController` owns the current call state and subscribes to call hub events. `CallWindowCoordinator` opens and focuses the WPF call window when call state requires it.

The call window handles the call UI and media controls. Lower-level audio capture and playback live in Infrastructure.

## Connection Status UI

`AppRealtimeCoordinator` connects hub state to `ConnectionStatusBar`.

The status bar is hidden when the user is not authenticated. When authenticated, it can show connected or reconnecting states based on `HubService.ConnectionStateChanged` and `HubService.IsReconnecting`.

## Current Limitations

* Some call and chat flows still subscribe to hub events from presentation code.
* The main hub connection is app-level, while the call connection is on-demand; call diagnostics should check both states.
* Realtime payload parsing still handles some loosely shaped objects for compatibility.
* Reconnection state is local to the client process and should not be treated as delivery confirmation.

## Related Documents

* [Client Architecture](ARCHITECTURE.md)
* [API Clients](API_CLIENTS.md)
* [Caching](CACHING.md)
* [Theming](THEMING.md)
* [Server Realtime](../server/REALTIME.md)
