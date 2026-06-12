# Shared Contracts

This document describes the role and maintenance rules of the Edemly.Contracts project.

The goal of this document is to explain why shared contracts exist, what belongs in this project, and how changes to contracts should be handled.

## Contents

* [Overview](#overview)
* [Responsibilities](#responsibilities)
* [Project Structure](#project-structure)
* [Contract Types](#contract-types)
* [Validation](#validation)
* [Compatibility Rules](#compatibility-rules)
* [Design Rules](#design-rules)
* [Maintenance Notes](#maintenance-notes)
* [Related Documents](#related-documents)

## Overview

Edemly.Contracts is a shared project referenced by both Edemly.Server and Edemly.Client.

It defines the data models used at the communication boundary between the backend and the desktop client. These models are used for HTTP API requests, HTTP API responses, and SignalR realtime payloads.

The main reason for keeping these models in a separate project is consistency. The server and the client should not maintain separate copies of the same request, response, or realtime payload models.

## Responsibilities

Edemly.Contracts is responsible for defining data shapes exchanged between the server and the client.

It is used for:

* API request models.
* API response models.
* Shared DTOs used by API clients and controllers.
* SignalR realtime event payloads.
* File upload response models.
* Payment-related communication models.
* Shared message type constants used by chat and call system messages.

The project should stay focused on communication contracts. It should not become a place for application logic, UI models, database entities, API clients, service implementations, or infrastructure code.

## Project Structure

The project is organized by feature area.

| Folder      | Responsibility                               |
| ----------- | -------------------------------------------- |
| Auth        | Authentication and session-related contracts |
| Calls       | Call metadata, participant, lifecycle, and system-message contracts |
| ChatMembers | Chat membership contracts                    |
| Chats       | Chat contracts                               |
| Companies   | Company workspace and admin contracts        |
| Files       | File upload contracts                        |
| Messages    | Message contracts                            |
| Notes       | Contact note contracts                       |
| Payments    | Payment and subscription contracts           |
| Realtime    | SignalR event payloads                       |
| Remindings  | Reminder and task contracts                  |
| Users       | User-related contracts                       |

Generated folders such as bin and obj are build output and are not part of the source contract structure.

## Contract Types

The project contains several types of contracts.

| Type         | Purpose                                     |
| ------------ | ------------------------------------------- |
| Request DTO  | Data sent from the client to the server     |
| Response DTO | Data returned from the server to the client |
| General DTO  | Shared data model used by both sides        |
| Realtime DTO | SignalR event payload                       |

Request and response contracts should be separated when the client input and server output have different purposes or different fields.

General DTOs are acceptable when the same shape is naturally used in multiple places, for example when displaying a user, chat, or message.

Realtime DTOs should be kept stable because both server hub handlers and client SignalR handlers depend on them.

Call-related realtime contracts are participant-oriented. They include direct and group call payloads, participant updates, accepted/rejected events, and shared call metadata constants for `Direct`/`Group` scopes and `Audio`/`Video` media kinds.

## Validation

Some contracts may use validation attributes from System.ComponentModel.DataAnnotations.

Validation attributes are appropriate for simple input rules such as:

* required values;
* maximum string lengths;
* email format validation;
* numeric ranges.

Validation attributes should not replace business validation.

Business rules should remain in the server application layer. For example, permissions, ownership checks, payment state transitions, chat membership rules, and tenant-specific rules should not be implemented inside contracts.

## Compatibility Rules

Contracts are part of the client-server boundary. Changing them can break communication between the server and the client.

Prefer additive changes over breaking changes. Adding an optional property or a new DTO is usually safer than renaming existing properties, changing property types, removing fields used by the client, or changing serialized enum values.

When a contract change affects both projects, update the server and client together when possible.

## Design Rules

Contracts should stay simple and stable.

Recommended rules:

* Keep contracts as plain data models.
* Keep contracts free from business logic.
* Do not expose EF Core entities as API contracts.
* Do not reference WPF-specific types.
* Do not reference ASP.NET Core controller, hub, middleware, or infrastructure types.
* Do not place API clients or service interfaces in this project.
* Avoid adding fields that are not needed by the client.
* Prefer explicit request and response models for operations with different input and output shapes.
* Keep realtime payloads compatible with both server and client handlers.
* Treat public property names as part of the API contract.

## Maintenance Notes

The contracts project should be reviewed when API or realtime functionality changes.

Recommended maintenance rules:

* Remove unused DTOs when related endpoints or features are removed.
* Avoid creating duplicate DTOs with the same purpose.
* Keep folder names aligned with feature names used by the server and client.
* Review realtime DTOs together with SignalR hub methods and client handlers.
* Avoid committing generated build folders such as bin and obj as source content.

The current project already benefits from a separate contracts project, but naming consistency can still be improved gradually as the API stabilizes.

## Related Documents

* [Shared Documentation](README.md)
* [Server Architecture](../server/ARCHITECTURE.md)
* [Server API](../server/API.md)
* [Server Realtime](../server/REALTIME.md)
* [Client Documentation](../client/README.md)
