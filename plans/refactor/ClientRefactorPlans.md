# Client Refactor Plans

This folder is the working area for ongoing refactor plans.

## Current Block

- Branch: `refactor/client-rendering-presentation`
- Scope: move rendering-oriented helpers out of `Edemly.Client/UI/Helpers` into `Edemly.Client/Presentation/Rendering/*`
- Goal: align the client structure with the target presentation grouping without changing behavior

## Current Cleanup Block

- Branch: `refactor/client-remove-ui-helpers`
- Scope: remove the remaining legacy `UI/Helpers` and root `Helpers` placements
- Targets:
  - `ChatLoader` -> `Application/Chats`
  - `ChatViewModel` -> `Presentation/ViewModels/Chats`
  - `UserSearchHandler` -> presentation-level chat rendering/search placement
  - `UrlHelper` -> non-helper module placement
- Goal: make `UI` and `Helpers` unnecessary folders in the client tree

## Temporary Decisions

- `Edemly.Client/GlobalUsings.cs` is a temporary bridge for the namespace migration.
- Keep it only while folders and namespaces are still being realigned.
- As modules stabilize, replace broad global imports with local `using` directives where they improve clarity.

## Next Candidate Blocks

- Move `ChatLoader` and `UserSearchHandler` out of `UI/Helpers`
- Split `MessageRenderer` by message type
- Extract chat list state and sorting from `ChatManager`
