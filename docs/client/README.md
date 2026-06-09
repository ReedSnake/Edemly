# Client Documentation

This section contains documentation related to the Edemly desktop application.

The client is built with WPF and is responsible for user interaction, realtime communication, local caching, theming, API integration, and application state management.

## Contents

* [Architecture](#architecture)
* [UI Structure](#ui-structure)
* [API Clients](#api-clients)
* [Realtime Communication](#realtime-communication)
* [Caching](#caching)
* [Theming](#theming)
* [Testing](#testing)
* [Related Resources](#related-resources)

## [Architecture](ARCHITECTURE.md)

Describes the overall client structure, architectural organization, and responsibility boundaries.

Topics include:

* Project structure
* Application layers
* Service organization
* Navigation flow
* Dependency flow
* Client architecture principles

---

## [UI Structure](UI_STRUCTURE.md)

Describes the organization of the user interface and presentation layer.

Topics include:

* Pages
* Dialogs
* Controls
* Resources
* Navigation
* UI organization

---

## [API Clients](API_CLIENTS.md)

Describes communication between the client and server.

Topics include:

* ApiClientBase
* API client organization
* Request flow
* DTO usage
* Error handling
* Authentication integration

---

## [Realtime Communication](REALTIME.md)

Describes realtime communication with the server.

Topics include:

* SignalR clients
* Hub connections
* Connection lifecycle
* Message updates
* Voice call signaling
* Realtime events

---

## [Caching](CACHING.md)

Describes local data storage and caching behavior.

Topics include:

* Profile picture caching
* File caching
* Local storage
* Cache invalidation
* Runtime data storage

---

## [Theming](THEMING.md)

Describes application styling and theme management.

Topics include:

* ThemeService
* Resource dictionaries
* Shared styles
* Dynamic resources
* Theme switching

---

## [Testing](TESTING.md)

Describes the testing approach used by the client.

Topics include:

* Test structure
* Client test project
* Test execution
* Testing strategy

## Related Resources

* [Documentation Home](../README.md)
* [Project README](../../README.md)
* [Client Project](../../Edemly.Client/)
