# Server Realtime

This document describes the SignalR realtime surface exposed by the Edemly server.

Realtime behavior is part of the client contract. Hub routes, hub methods, and client event names should stay stable unless the client is updated in the same planned change.

## Contents

* [Overview](#overview)
* [Authentication](#authentication)
* [Hub Routes](#hub-routes)
* [MainHub](#mainhub)
* [CallHub](#callhub)
* [Presence](#presence)
* [Message Consistency](#message-consistency)
* [Scale-Out Notes](#scale-out-notes)
* [Current Limitations](#current-limitations)
* [Related Documents](#related-documents)

## Overview

The server exposes SignalR hubs for chat, presence, reminders, profile updates, group updates, and call signaling.

The current public hub routes are:

| Route | Hub | Purpose |
| ----- | --- | ------- |
| `/main` | `MainHub` | Chat messages, chat/profile notifications, presence, and reminder confirmation |
| `/call` | `CallHub` | Call lifecycle, WebRTC signaling, mute state, and audio chunk relay |

Both hubs are authenticated with `[Authorize]`.

## Authentication

SignalR uses the same JWT model as protected HTTP endpoints.

The bearer configuration accepts tokens from the `access_token` query parameter for:

* `/main`
* `/call`
* legacy `/hubs`

`JwtUserIdProvider` resolves SignalR user ids from `ClaimTypes.NameIdentifier`, `userId`, or `sub`. Current generated JWTs include `userId`, and hub method code should continue to receive that claim.

## Hub Routes

Routes are mapped in `Program.cs`:

```csharp
endpoints.MapHub<MainHub>("/main");
endpoints.MapHub<CallHub>("/call");
```

The local Docker profile uses a dedicated hub gateway at `http://localhost:3700`, but it still proxies to the same `server1` process in the current local stack.

## MainHub

`MainHub` handles normal chat and app-wide realtime notifications.

Server methods called by the client:

| Hub method | Purpose |
| ---------- | ------- |
| `SendMessage` | Creates a message and broadcasts `ReceiveMessage` to chat members |
| `UpdateMessage` | Updates a message and broadcasts `ReceiveMessageUpdate` |
| `DeleteMessage` | Deletes a message and broadcasts `ReceiveMessageDelete` |
| `NotifyGroupCreated` | Broadcasts a group-created notification |
| `NotifyProfileUpdated` | Broadcasts profile picture updates |
| `NotifyGroupUpdated` | Broadcasts group metadata updates |
| `ConfirmRemindingReceived` | Confirms reminder delivery for the current user |

Client events sent by the server include:

| Event | Purpose |
| ----- | ------- |
| `ReceiveMessage` | New chat message or call system message |
| `ReceiveMessageUpdate` | Edited message or updated call system message |
| `ReceiveMessageDelete` | Deleted chat message |
| `GroupCreated` | Group chat was created |
| `ProfileUpdated` | User profile picture changed |
| `GroupUpdated` | Group metadata or icon changed |
| `UserStatusChanged` | User online/offline presence changed |

Message send/update/delete methods check chat membership and ownership or chat role before writing and broadcasting.

## CallHub

`CallHub` handles call lifecycle and signaling.

Server methods called by the client:

| Hub method | Purpose |
| ---------- | ------- |
| `StartCall` | Starts a direct or group call |
| `AcceptCall` | Accepts an incoming call |
| `RejectCall` | Rejects an incoming call |
| `EndCall` | Ends or leaves a call |
| `SetCallMuted` | Updates participant mute state |
| `SendOffer` | Sends a WebRTC offer to another user |
| `SendAnswer` | Sends a WebRTC answer to another user |
| `SendIceCandidate` | Sends ICE candidate data |
| `SendAudioChunk` | Relays an audio chunk to active recipients |

Call lifecycle methods delegate authorization and state changes to `CallService`. The service checks chat membership and participant state before returning notifications to broadcast.

Call methods may also emit chat message events when call system messages are created or updated.

## Presence

`MainHub` stores online state in `UserPresenceService`.

On connect, the hub marks the current user online and broadcasts `UserStatusChanged`.

On disconnect, the hub marks the user offline when appropriate and broadcasts `UserStatusChanged` with `lastSeen`.

Presence is currently in process. Multiple backend instances require a distributed presence store before presence can be trusted across instances.

## Message Consistency

Message history is also served through HTTP at `GET api/chats/{chatId}/messages`.

Realtime message writes update the same message table and maintain the chat last-message snapshot:

* `Chat.LastMessageId`
* `Chat.LastMessageText`
* `Chat.LastMessageSenderId`
* `Chat.LastMessageTime`

`MainHub.SendMessage` now commits message creation and snapshot update in one transaction. Message edit and delete paths refresh the snapshot when the edited or deleted message is the current last message.

`MainHub` still contains duplicate message write logic that overlaps with `MessageService`. A future refactor can move that logic behind an application-level message workflow while preserving the client realtime contract.

## Scale-Out Notes

The current local profile uses multiple gateways, but all gateways still route to one backend server instance.

Before running multiple backend instances behind the gateways, add distributed infrastructure for:

* SignalR backplane;
* presence state;
* verification code state if more than one server handles auth;
* cache invalidation for message cache entries;
* background work coordination if more hosted services are added.

Redis is the planned first option for the SignalR backplane and shared short-lived state.

## Current Limitations

* `MainHub` still duplicates message write mechanics from `MessageService`.
* Presence is in memory.
* Message cache invalidation is local to one process.
* Call audio chunk relay should be load-tested before using a Redis backplane for call traffic.
* SignalR method and event names are client contracts and should not be renamed casually.

## Related Documents

* [Server API](API.md)
* [Server Authentication](AUTH.md)
* [Server Security](SECURITY.md)
* [Server Database](DATABASE.md)
* [Deployment](DEPLOYMENT.md)
* [Local Docker And Update Runbook](LOCAL_DOCKER_RUNBOOK.md)
