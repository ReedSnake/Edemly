# Server API

This document describes the HTTP API exposed by the Edemly server application.

The goal of this document is to provide a structured overview of available REST endpoints, their purpose, and their authentication requirements.

This document does not describe authentication internals, database structure, file storage implementation, or SignalR realtime events. These topics are documented separately.

## Contents

* [Overview](#overview)
* [API Conventions](#api-conventions)
* [Swagger / OpenAPI](#swagger--openapi)
* [Authentication](#authentication)
* [Response Model](#response-model)
* [Endpoint Summary](#endpoint-summary)
* [Auth Endpoints](#auth-endpoints)
* [User Endpoints](#user-endpoints)
* [Chat Endpoints](#chat-endpoints)
* [Chat Member Endpoints](#chat-member-endpoints)
* [Message Endpoints](#message-endpoints)
* [Note Endpoints](#note-endpoints)
* [Reminding Endpoints](#reminding-endpoints)
* [File Endpoints](#file-endpoints)
* [Payment Endpoints](#payment-endpoints)
* [Company Admin Endpoints](#company-admin-endpoints)
* [Implementation Notes](#implementation-notes)
* [Related Documents](#related-documents)

## Overview

Edemly Server exposes HTTP endpoints for authentication, users, chats, chat members, messages, notes, reminders, files, payments, and company administration.

The API is used by the WPF desktop client.

Most application operations are exposed through REST-style controllers. Realtime communication is handled separately through SignalR hubs and is documented in [REALTIME.md](REALTIME.md).

Call lifecycle operations are not exposed through an HTTP controller. They currently run through the authenticated `/call` SignalR hub and the server-side `CallService`.

## API Conventions

The API uses JSON request and response bodies for most endpoints.

Common conventions:

| Convention     | Description                                                             |
| -------------- | ----------------------------------------------------------------------- |
| Base prefix    | Most endpoints are under the api path                                   |
| Authentication | Protected endpoints require JWT Bearer authentication                   |
| Request body   | Create and update operations usually receive DTOs from Edemly.Contracts |
| File upload    | File endpoints use multipart form-data                                  |
| Pagination     | Message retrieval supports page and pageSize query parameters           |
| Responses      | Most application controllers return service results converted to HTTP responses |

Routes are documented as they currently exist in the server controllers.

## Swagger / OpenAPI

The server exposes Swagger / OpenAPI in development mode.

Swagger should be used as the main source for detailed request and response schemas, parameter names, request body structure, and DTO shapes.

This document intentionally does not duplicate JSON request or response examples. It provides a readable API map, while Swagger and Edemly.Contracts provide the detailed contract information.

When API contracts change, Swagger and Edemly.Contracts should be treated as the detailed source of truth. This document should remain a high-level overview of available HTTP endpoints.

## Authentication

Protected endpoints require a valid JWT token.

The client should send the token through the Authorization header using the Bearer scheme.

Authentication details, JWT claims, session tokens, email verification, and SignalR authentication are described in AUTH.md.

In endpoint tables, the Auth column uses the following values:

| Value     | Meaning                              |
| --------- | ------------------------------------ |
| Public    | No JWT required                      |
| Protected | JWT required                         |
| Admin     | JWT required and Admin role required |

## Response Model

Most application controllers use service result objects internally and convert them to HTTP responses.

Common response types include:

| Status           | Meaning                                            |
| ---------------- | -------------------------------------------------- |
| 200 OK           | Operation completed successfully                   |
| 201 Created      | Resource was created                               |
| 204 No Content   | Operation completed without response body          |
| 400 Bad Request  | Invalid request or validation error                |
| 401 Unauthorized | Authentication is missing or invalid               |
| 403 Forbidden    | User is authenticated but does not have permission |
| 404 Not Found    | Requested resource was not found                   |
| 409 Conflict     | Resource conflict, such as duplicate data          |
| 500 Server Error | Unexpected server-side error                       |

Exact response bodies may differ by endpoint and are defined by server controllers and shared contracts.

## Endpoint Summary

| Area         | Main route                                      |
| ------------ | ----------------------------------------------- |
| Auth         | api/auth                                        |
| Users        | api/users                                       |
| Chats        | api/chats                                       |
| Chat members | api/chat-members and api/chats/{chatId}/members |
| Messages     | api/chats/{chatId}/messages                     |
| Notes        | api/users/{targetUserId}/note and api/notes     |
| Remindings   | api/remindings                                  |
| Files        | api/files                                       |
| Payments     | api/Payment                                     |
| Companies    | api/admin/companies                             |
| Realtime hubs | /main and /call                                 |

## Auth Endpoints

Authentication endpoints are responsible for email-code authentication, registration, session restoration, and logout.

| Method | Route                  | Auth      | Purpose                                            |
| ------ | ---------------------- | --------- | -------------------------------------------------- |
| POST   | api/auth/get-code      | Public    | Sends a verification code to an email address      |
| POST   | api/auth/login         | Public    | Logs in using email and verification code          |
| POST   | api/auth/register      | Public    | Registers a user using email and verification code |
| POST   | api/auth/session-login | Public    | Restores authentication using a session token      |
| POST   | api/auth/logout        | Protected | Logs out the current user                          |

Authentication model details are documented in AUTH.md.

## User Endpoints

User endpoints are responsible for retrieving, searching, updating, and deleting users.

| Method | Route                          | Auth      | Purpose                                        |
| ------ | ------------------------------ | --------- | ---------------------------------------------- |
| GET    | api/users/me                   | Protected | Gets the current authenticated user            |
| GET    | api/users/{userId}             | Public    | Gets a user by id                              |
| GET    | api/users/search?query={query} | Protected | Searches users by query                        |
| POST   | api/users/batch                | Protected | Gets multiple users by ids                     |
| PUT    | api/users/me                   | Protected | Updates the current authenticated user         |
| DELETE | api/users/me                   | Protected | Deletes the current authenticated user         |
| POST   | api/users/me/profile-picture   | Protected | Uploads a profile picture for the current user |

Profile picture upload uses multipart form-data and has a request size limit.

## Chat Endpoints

Chat endpoints are responsible for private chats, group chats, chat retrieval, and chat updates.

| Method | Route                   | Auth      | Purpose                         |
| ------ | ----------------------- | --------- | ------------------------------- |
| POST   | api/chats/private       | Protected | Creates or gets a private chat  |
| POST   | api/chats/group         | Protected | Creates a group chat            |
| GET    | api/chats               | Protected | Gets chats for the current user |
| GET    | api/chats/{chatId}      | Protected | Gets a chat by id               |
| PUT    | api/chats/{chatId}      | Protected | Updates chat information        |
| POST   | api/chats/{chatId}/icon | Protected | Uploads or updates a chat icon  |

Chat icon upload uses multipart form-data and has a request size limit.

Chat detail and chat list responses are scoped to the current authenticated user. Chat metadata and icon updates require the current user to have a chat role that can update the chat.

## Chat Member Endpoints

Chat member endpoints are responsible for retrieving and managing chat membership.

| Method | Route                           | Auth      | Purpose                              |
| ------ | ------------------------------- | --------- | ------------------------------------ |
| GET    | api/chat-members/{chatMemberId} | Protected | Gets a chat member by id             |
| GET    | api/chats/{chatId}/members      | Protected | Gets members of a chat               |
| GET    | api/chat-members/me             | Protected | Gets memberships of the current user |
| POST   | api/chats/{chatId}/members      | Protected | Adds a member to a chat              |
| PUT    | api/chat-members/{chatMemberId} | Protected | Updates a chat member                |
| DELETE | api/chat-members/{chatMemberId} | Protected | Removes a chat member                |

Permission checks for chat membership operations should be handled by application services.

The `api/chat-members/{chatMemberId}` lookup requires the current user to belong to the same chat as the requested member.

## Message Endpoints

Message endpoints are responsible for retrieving chat messages.

| Method | Route                                                       | Auth      | Purpose                            |
| ------ | ----------------------------------------------------------- | --------- | ---------------------------------- |
| GET    | api/chats/{chatId}/messages?page={page}&pageSize={pageSize} | Protected | Gets paginated messages for a chat |

Message retrieval supports pagination.

| Parameter | Purpose                     |
| --------- | --------------------------- |
| page      | Page number                 |
| pageSize  | Number of messages per page |

Message creation, editing, deletion, and realtime delivery should be documented here only if corresponding HTTP endpoints exist.

Message reads require the current user to have access to the message's chat. Message create, edit, delete, and broadcast currently happen through `MainHub` and are documented in [REALTIME.md](REALTIME.md).

## Note Endpoints

Note endpoints are responsible for contact notes and note counts.

| Method | Route                         | Auth      | Purpose                                         |
| ------ | ----------------------------- | --------- | ----------------------------------------------- |
| GET    | api/users/{targetUserId}/note | Protected | Gets the current user's note about another user |
| PUT    | api/users/{targetUserId}/note | Protected | Saves or updates a note about another user      |
| DELETE | api/users/{targetUserId}/note | Protected | Deletes a note about another user               |
| GET    | api/notes/count               | Protected | Gets the current user's note count              |

Contact notes are user-specific. A note about the same target user belongs to the current authenticated user.

## Reminding Endpoints

Reminding endpoints are responsible for reminder and task operations.

| Method | Route                                   | Auth      | Purpose                              |
| ------ | --------------------------------------- | --------- | ------------------------------------ |
| GET    | api/remindings                          | Protected | Gets remindings for the current user |
| GET    | api/remindings/{remindingId}            | Protected | Gets a reminding by id               |
| POST   | api/remindings                          | Protected | Creates a reminding                  |
| PUT    | api/remindings/{remindingId}            | Protected | Updates a reminding                  |
| PATCH  | api/remindings/{remindingId}/completion | Protected | Toggles reminding completion state   |
| DELETE | api/remindings/{remindingId}            | Protected | Deletes a reminding                  |

Reminder-specific business rules should be handled in the application layer.

## File Endpoints

File endpoints are responsible for uploading, downloading, and deleting files.

| Method | Route                                | Auth      | Purpose                      |
| ------ | ------------------------------------ | --------- | ---------------------------- |
| POST   | api/files                            | Protected | Uploads a file               |
| GET    | api/files/download?fileUrl={fileUrl} | Protected | Downloads a file by file URL |
| DELETE | api/files?fileUrl={fileUrl}          | Protected | Deletes a file by file URL   |
| GET    | /uploads/{**filePath}                | Protected | Reads an uploaded file path   |
| GET    | /{company}/uploads/{**filePath}      | Protected | Reads a tenant upload path    |

File upload uses multipart form-data and has a request size limit.

File storage structure, upload folders, MinIO/local provider behavior, authenticated upload access, and storage-related risks are documented in FILE_STORAGE.md.

## Payment Endpoints

Payment endpoints are responsible for starting payments, receiving payment returns, retrieving payment history, and checking payment status.

| Method | Route                                | Auth      | Purpose                                   |
| ------ | ------------------------------------ | --------- | ----------------------------------------- |
| GET    | api/Payment/initiate?amount={amount} | Protected | Starts a payment flow                     |
| POST   | api/Payment/return                   | Public    | Handles return from payment provider      |
| GET    | api/Payment/history                  | Protected | Gets payment history for the current user |
| GET    | api/Payment/status/{orderId}         | Protected | Checks payment status by order id         |

The current route uses api/Payment because the controller route is based on the controller name.

For consistency with the rest of the API, this route may be renamed later to api/payments.

Payment history is scoped to the current authenticated user. Payment status checks reject order references that do not belong to the current user.

The payment return endpoint is public because the provider posts back to it. It must complete the payment by transaction/order reference and upgrade the user attached to that payment record, not a user id supplied by the caller.

Payment provider details should be documented in deployment notes or a payment-specific document if the payment flow becomes more complex.

## Company Admin Endpoints

Company admin endpoints are responsible for company workspace management.

| Method | Route                                  | Auth             | Purpose                          |
| ------ | -------------------------------------- | ---------------- | -------------------------------- |
| GET    | api/admin/companies                    | Public currently | Gets companies                   |
| POST   | api/admin/companies                    | Admin            | Creates a company workspace      |
| POST   | api/admin/companies/{companyId}/emails | Admin            | Adds allowed emails to a company |

The GET companies endpoint is currently not protected in the controller, although it is under the admin route prefix.

This should be reviewed. If the endpoint is intended for admin use, it should require Admin authorization.

Company database provisioning and tenant database behavior are documented in DATABASE.md.

## Implementation Notes

The following notes are based on the current controller structure and should be reviewed as the API stabilizes.

### Payment route naming

Payment routes currently use api/Payment.

Most other routes use lowercase plural naming, such as api/users, api/chats, api/files, and api/remindings.

Recommended future route:

| Current     | Recommended  |
| ----------- | ------------ |
| api/Payment | api/payments |

### Company authorization

api/admin/companies GET is currently public.

Because the route is under api/admin, it should be reviewed and either documented as intentionally public or protected with Admin authorization.

### Authenticated file download

api/files/download is protected.

Direct `/uploads/...` paths are also protected by middleware and controller routing. The storage model still uses URL strings as file identifiers, so detailed access-control limitations are documented in FILE_STORAGE.md and SECURITY.md.

### Public user profile endpoint

api/users/{userId} is currently public.

This is acceptable if public profiles are intended. If user profiles should only be visible to authenticated users, the endpoint should require authentication.

### Swagger as detailed API reference

This document intentionally does not include full JSON request and response examples.

Use Swagger / OpenAPI for detailed endpoint schemas and Edemly.Contracts for shared DTO definitions.

### Payment status verification

`WayForPayService.CheckPaymentStatusAsync` currently remains a stub-like implementation. Payment status verification is not production-ready until real provider verification is implemented and tested.

## Related Documents

* [Server Architecture](ARCHITECTURE.md)
* [Server Authentication](AUTH.md)
* [Server Security](SECURITY.md)
* [Server Database](DATABASE.md)
* [File Storage](FILE_STORAGE.md)
* [Server Realtime](REALTIME.md)
* [Shared Contracts](../shared/CONTRACTS.md)
