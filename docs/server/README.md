# Server Documentation

This section contains documentation related to the Edemly backend application.

The server is built with ASP.NET Core and provides REST APIs, SignalR realtime communication, authentication, file storage, multi-tenant database management, and business services used by the desktop client.

## Contents

* [Architecture](#architecture)
* [Database](#database)
* [Authentication](#authentication)
* [Security](#security)
* [API](#api)
* [Realtime Communication](#realtime-communication)
* [File Storage](#file-storage)
* [Testing](#testing)
* [Deployment](#deployment)
* [Local Docker Runbook](#local-docker-runbook)
* [Client Releases](#client-releases)
* [Safe Optimization Backlog](#safe-optimization-backlog)
* [Related Resources](#related-resources)

## [Architecture](ARCHITECTURE.md)

Describes the overall server structure, architectural layers, project organization, service boundaries, request flow, and technology stack.

Topics include:

* Project-wide architecture principles
* Solution structure
* Layer responsibilities
* Dependency flow
* Request lifecycle
* Service organization
* External dependencies

---

## [Database](DATABASE.md)

Describes database architecture and data storage.

Topics include:

* Server database
* Company databases
* Multi-tenant architecture
* Entity Framework Core
* Migrations
* Tenant provisioning

---

## [Authentication](AUTH.md)

Describes authentication and authorization mechanisms.

Topics include:

* Email-code login flow
* JWT token generation
* Authorization
* Current user resolution
* Security considerations

---

## [Security](SECURITY.md)

Summarizes the current server-side security boundaries and known hardening gaps.

Topics include:

* Protected endpoints and JWT claim usage
* Chat, message, file, payment, and company authorization rules
* Realtime access rules
* Transaction boundaries for partial-save prevention
* Current production-hardening gaps

---

## [API](API.md)

Describes the REST API exposed by the server.

Topics include:

* Endpoint organization
* Request and response conventions
* Authentication requirements
* Resource ownership rules
* Error responses

---

## [Realtime Communication](REALTIME.md)

Describes SignalR communication between clients and the server.

Topics include:

* Hub routes and authentication
* `MainHub` methods and events
* `CallHub` lifecycle and signaling
* Presence behavior
* Message snapshot consistency
* Redis scale-out notes

---

## [File Storage](FILE_STORAGE.md)

Describes file upload and storage behavior.

Topics include:

* Local and MinIO-backed storage modes
* Authenticated file access
* Upload workflow
* Profile pictures
* Attachments
* Tenant path behavior

---

## [Testing](TESTING.md)

Describes the testing approach used by the server.

Topics include:

* Integration tests
* Test infrastructure
* SQLite in-memory database
* Message/history/snapshot coverage
* Security, payment, and file access coverage
* Running tests and build checks

---

## [Deployment](DEPLOYMENT.md)

Describes deployment and environment configuration.

Topics include:

* Configuration
* Environment variables
* Database setup
* Hosting
* File storage provider settings
* Payment and Redis production cautions
* Production considerations

---

## [Local Docker Runbook](LOCAL_DOCKER_RUNBOOK.md)

Step-by-step local testing guide for Docker startup, static bootstrap, installer downloads, optional updates, mandatory updates, gateway fallback, and Redis scale-out planning.

Topics include:

* Running the local Docker stack
* Publishing client updates without restarting the static container
* Testing optional and mandatory updates
* Testing gateway fallback
* Redis backplane and distributed state analysis

---

## [Client Releases](RELEASES.md)

Describes local Windows client release creation and update hosting.

Topics include:

* Static `client.json` update metadata
* Optional and mandatory update behavior
* Velopack packaging
* Local static download site
* Docker URLs for API, payment, and hubs

---

## [Safe Optimization Backlog](SAFE_OPTIMIZATION_BACKLOG.md)

Lists safe remaining server optimizations after the current chat/message performance pass.

Topics include:

* Message history and chat snapshot tests
* Hub/service message write cleanup
* Remaining transaction and authorization coverage
* Payment and file-storage hardening
* Safe index review rules

## Related Resources

* [Documentation Home](../README.md)
* [Architecture Principles](../ARCHITECTURE_PRINCIPLES.md)
* [Project README](../../README.md)
* [Server Project](../../Edemly.Server/)
