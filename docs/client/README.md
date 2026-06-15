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

Describes the overall client structure, architectural organization, startup model, and responsibility boundaries.

Topics include:

* Project-wide architecture principles
* WPF-first layered architecture
* Project structure
* Application layers
* Service organization
* Startup and composition root behavior
* Dependency flow
* Current limitations

---

## [UI Structure](UI_STRUCTURE.md)

Describes the organization of the user interface and presentation layer.

Topics include:

* Presentation folder layout
* Pages and windows
* Dialogs and controls
* Rendering helpers
* Chat workspace controller
* Navigation boundaries

---

## [API Clients](API_CLIENTS.md)

Describes communication between the client and server.

Topics include:

* Shared API client context
* API client organization
* Request flow
* DTO usage
* Error handling
* Authentication integration

---

## [Realtime Communication](REALTIME.md)

Describes realtime communication with the server.

Topics include:

* Main and call hub connections
* Connection lifecycle
* Chat and call events
* Chat workspace integration
* Connection status UI

---

## [Caching](CACHING.md)

Describes local data storage and caching behavior.

Topics include:

* Chat cache
* Profile picture and file caches
* Configuration storage
* Secure token storage
* Cache scopes
* Cache invalidation

---

## [Theming](THEMING.md)

Describes application styling and theme management.

Topics include:

* ThemeService
* Resource dictionaries
* Shared styles
* Dynamic resources
* Theme switching
* Themed page/window/control lifecycle

---

## [Testing](TESTING.md)

Describes the testing approach used by the client.

Topics include:

* Test structure
* Client test project
* Test execution
* Current coverage
* Recommended coverage
* Manual checks

## Related Resources

* [Documentation Home](../README.md)
* [Architecture Principles](../ARCHITECTURE_PRINCIPLES.md)
* [Project README](../../README.md)
* [Client Project](../../Edemly.Client/)
