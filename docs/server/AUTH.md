# Server Authentication

This document describes the authentication and authorization model used by the Edemly server application.

The goal of this document is to explain how users authenticate, how JWT and session tokens are used, how the server identifies the current user, and how authentication works with company-specific tenant databases.

This document does not describe individual API endpoints. Endpoint routes, request bodies, response models, and status codes should be documented in API.md.

## Contents

* [Overview](#overview)
* [Authentication Model](#authentication-model)
* [Email Verification](#email-verification)
* [JWT and Session Tokens](#jwt-and-session-tokens)
* [User Identity](#user-identity)
* [Roles and Authorization](#roles-and-authorization)
* [Tenant-Aware Authentication](#tenant-aware-authentication)
* [SignalR Authentication](#signalr-authentication)
* [Configuration](#configuration)
* [Security Notes](#security-notes)
* [Current Limitations](#current-limitations)
* [Related Documents](#related-documents)

## Overview

Edemly Server uses JWT-based authentication.

The current authentication model is based on email verification codes. Users register and log in using their email address and a verification code sent to that email.

Email is the primary authentication identifier in Edemly.

Username and phone number may also be unique in the database, but they are profile-level values. They can be changed later and are not used as the primary login identifier.

After successful authentication, the server returns an authentication response with:

| Value         | Purpose                                         |
| ------------- | ----------------------------------------------- |
| JWT token     | Short-lived access token for protected requests |
| Session token | Longer-lived token for restoring a user session |
| User id       | Internal authenticated user identifier          |
| Username      | Public user name shown in the application       |
| Email         | Email address used for authentication           |

## Authentication Model

The server does not currently use password-based authentication.

Authentication is handled mainly by AuthService.

Supporting authentication components include:

| Component           | Responsibility                                                               |
| ------------------- | ---------------------------------------------------------------------------- |
| AuthService         | Handles login, registration, verification code validation, and session login |
| AuthResponseFactory | Creates authentication responses and manages session token creation or reuse |
| JwtService          | Generates and validates JWT tokens                                           |
| Email service       | Generates, sends, and verifies email codes                                   |
| Tenant provider     | Provides current company context for tenant-specific authentication          |

The general authentication model is:

1. The client requests a verification code for an email address.
2. The server validates the email and tenant rules if needed.
3. The server sends a verification code.
4. The client submits the email and verification code.
5. The server verifies the code.
6. The server creates or finds the user account depending on the operation.
7. The server returns a JWT token and a session token.

The exact API routes for requesting a code, logging in, registering, and restoring a session are described in API.md.

## Email Verification

Email verification is used for both login and registration.

For global authentication, the server validates the email format and sends a verification code.

For company-specific authentication, the server also checks whether the email is allowed for the current company workspace.

This means that company registration can be restricted to approved email addresses.

The email service can work in different modes depending on configuration. In development or testing, a mock email service can be used. In production-like environments, the server can use a real email provider.

Email verification is part of the authentication process, but it does not replace authorization. After a user is authenticated, the server still needs to check whether that user is allowed to perform a specific operation.

## JWT and Session Tokens

The server uses two token types.

| Token         | Purpose                                                            |
| ------------- | ------------------------------------------------------------------ |
| JWT token     | Used to access protected HTTP endpoints and SignalR hubs           |
| Session token | Used to restore authentication without requesting a new email code |

The JWT token is short-lived. Its lifetime is configured through Jwt ExpiresInMinutes.

The session token is longer-lived. Its lifetime is configured through Jwt RefreshTokenExpiresInDays.

Session login is used when the client already has a valid session token and needs a fresh authentication response.

A session token is not a replacement for JWT validation. It is used only to obtain a new JWT token and restore the authenticated session.

## User Identity

Email is the primary authentication identifier.

After authentication, the server uses the user id as the main internal identity value.

The generated JWT contains identity claims such as:

| Claim       | Purpose                                              |
| ----------- | ---------------------------------------------------- |
| sub         | User identifier                                      |
| unique_name | Username                                             |
| email       | User email                                           |
| jti         | Unique token identifier                              |
| userId      | User identifier used by controllers and server logic |
| role        | User role                                            |

Controllers usually read the current user id from the userId claim.

The user id is used internally for operations such as:

* loading the current user;
* checking ownership;
* checking chat membership;
* creating messages;
* saving user-specific data;
* applying permission rules.

Username and phone number can be unique, but they should be treated as profile fields rather than authentication identifiers.

## Roles and Authorization

The server currently uses a simple role model.

| Role  | Purpose                         |
| ----- | ------------------------------- |
| User  | Default authenticated user role |
| Admin | Administrative role             |

The Admin role is assigned when the authenticated email matches the configured admin email.

Authorization is handled with a combination of:

* JWT authentication;
* role checks for admin-only behavior;
* current user id checks;
* application-level permission checks.

Authentication answers who the user is.

Authorization answers what the user is allowed to do.

Feature-specific permission rules should stay in application services or permission services. They should not be implemented inside shared contracts or DTOs.

## Tenant-Aware Authentication

Authentication is tenant-aware.

If a request is associated with a company workspace, authentication uses the selected company database.

If no company tenant is resolved, authentication uses the main server database.

| Request context          | Database used             |
| ------------------------ | ------------------------- |
| Global request           | ServerDbContext           |
| Company-specific request | Selected CompanyDbContext |

For company-specific registration and verification code requests, the email must be allowed for the selected company.

This prevents users from registering inside a company workspace with an email address that is not approved for that company.

Tenant database structure, tenant provisioning, and database resolution are described in DATABASE.md.

## SignalR Authentication

SignalR connections also use JWT authentication.

For hub connections, the token can be passed through the access_token query parameter.

This is used because WebSocket clients commonly cannot send authorization headers in the same way as normal HTTP requests.

SignalR user identification is handled by JwtUserIdProvider. It resolves the connected user id from available JWT claims.

Realtime behavior, hub events, and SignalR-specific flows are described in REALTIME.md.

## Configuration

Authentication depends on several configuration values.

| Setting                       | Purpose                                             |
| ----------------------------- | --------------------------------------------------- |
| Jwt Issuer                    | Expected token issuer                               |
| Jwt Audience                  | Expected token audience                             |
| Jwt Key                       | Secret key used to sign and validate JWT tokens     |
| Jwt ExpiresInMinutes          | JWT lifetime                                        |
| Jwt RefreshTokenExpiresInDays | Session token lifetime                              |
| AdminEmail                    | Email address that receives the Admin role          |
| Brevo ApiKey                  | Email provider configuration for verification codes |

Secrets should not be committed to the repository.

JWT keys, email provider keys, database credentials, and production secrets should be stored through environment variables, user secrets, deployment platform secrets, or CI/CD secret storage.

## Security Notes

Authentication-related code should follow these rules:

* Do not expose JWT signing keys.
* Do not commit real email provider secrets.
* Do not log verification codes in production.
* Do not expose session tokens in logs.
* Treat session tokens as sensitive credentials.
* Validate company-specific email restrictions before creating tenant users.
* Keep permission checks close to application operations, not only in controllers.
* Use role-based authorization only for operations that are truly administrative.

## Current Limitations

The current authentication model is suitable for the current project state, but several areas should be reviewed later:

* Admin role assignment currently depends on a configured admin email.
* Session token storage and invalidation should be reviewed before production use.
* Logout behavior should be clearly defined if session management becomes more complex.
* Verification code expiration, retry limits, and abuse protection should be reviewed for production security.
* Tenant authentication rules should be tested carefully to avoid cross-tenant access issues.
* Role and permission rules may need a more explicit model if the number of roles grows.

## Related Documents

* [Server Architecture](ARCHITECTURE.md)
* [Server API](API.md)
* [Server Database](DATABASE.md)
* [Server Realtime](REALTIME.md)
* [Shared Contracts](../shared/CONTRACTS.md)
