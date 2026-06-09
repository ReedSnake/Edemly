# Server Documentation

This section contains documentation related to the Edemly backend application.

The server is built with ASP.NET Core and provides REST APIs, SignalR realtime communication, authentication, file storage, multi-tenant database management, and business services used by the desktop client.

## Contents

* [Architecture](#architecture)
* [Database](#database)
* [Authentication](#authentication)
* [API](#api)
* [Realtime Communication](#realtime-communication)
* [File Storage](#file-storage)
* [Testing](#testing)
* [Deployment](#deployment)
* [Related Resources](#related-resources)

## [Architecture](ARCHITECTURE.md)

Describes the overall server structure, architectural layers, project organization, service boundaries, request flow, and technology stack.

Topics include:

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

* SignalR hubs
* Client events
* Server events
* Chat updates
* Voice call signaling

---

## [File Storage](FILE_STORAGE.md)

Describes file upload and storage behavior.

Topics include:

* File storage locations
* Public file access
* Upload workflow
* Profile pictures
* Attachments

---

## [Testing](TESTING.md)

Describes the testing approach used by the server.

Topics include:

* Integration tests
* Test infrastructure
* SQLite in-memory database
* Test utilities
* Running tests

---

## [Deployment](DEPLOYMENT.md)

Describes deployment and environment configuration.

Topics include:

* Configuration
* Environment variables
* Database setup
* Hosting
* Production considerations

## Related Resources

* [Documentation Home](../README.md)
* [Project README](../../README.md)
* [Server Project](../../Edemly.Server/)
