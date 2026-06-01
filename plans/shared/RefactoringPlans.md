# Edemly Refactoring and Naming Roadmap

## Phase 1. Split the Largest Client Files

### Goal

Reduce risk in large UI and helper files.

### Largest Files Found

```text
uchat/Services/HubService.cs                  1376 lines
uchat/Pages/ChatManager_Core.cs               1335 lines
uchat/Pages/Page_calendar.xaml.cs             1303 lines
uchat/Pages/Page_main.xaml                    1231 lines
uchat/Pages/Page_calendar.xaml                1160 lines
uchat/Helpers/MessageRenderer.cs               958 lines
uchat/App.xaml.cs                              794 lines
```

### Tasks

* Split `HubService` by connection lifecycle, chat events, profile events, reminder events, and call events.
* Move calendar logic out of code-behind into smaller services/view-models.
* Break `MessageRenderer` into text, attachment, sticker, voice, and layout components.
* Keep XAML files focused by extracting reusable controls.
* Add focused tests around extracted non-UI logic.

### Result

Client changes become smaller, safer, and easier to review.

---

## Phase 2. Split Server Hotspots

### Goal

Make server behavior easier to reason about.

### Main Server Files

```text
uchat_server/Api/Controllers/AuthController.cs  593 lines
uchat_server/Hubs/MainHub.cs                    508 lines
uchat_server/Program.cs                         large startup pipeline
uchat_server/Api/Services/TenantProvisioningService.cs
```

### Tasks

* Split authentication, registration, session login, and tenant validation out of `AuthController`.
* Move SignalR message validation and tenant resolution out of `MainHub`.
* Move startup database migration and seeding code out of `Program.cs`.
* Split tenant provisioning into company record creation, database creation, migrations, and email allowlist management.
* Add service-level tests after extracting logic.

### Result

Server code has clearer ownership boundaries.

---

## Phase 3. Normalize File and Class Names

### Goal

Make file names predictable and easy to search.

### Rename Candidates

```text
uchat/Pages/Page_main_Core..cs       -> Page_main_Core.cs or PageMain.Core.cs
uchat_server/DeamonHelper.cs         -> DaemonHelper.cs
uchat/Assets/7 - Копировать.png      -> meaningful English asset name
uchat/Assets/image_setting (2).png   -> meaningful English asset name
uchat/Assets/Rectangle 460.png       -> meaningful English asset name
```

### Tasks

* Remove double dots, spaces, copy suffixes, and unclear numeric asset names.
* Use one naming convention for pages and partial classes.
* Rename assets to describe their role.
* Update references after each rename.
* Keep renames separate from behavioral changes when committing.

### Result

The project becomes easier to navigate and less fragile across tools.

---

## Phase 4. Normalize Namespaces

### Goal

Make namespaces match folder structure.

### Current Issue

Some controllers use `uchat_server.Controllers`, while others use `uchat_server.Api.Controllers`.

### Tasks

* Move all API controllers to `uchat_server.Api.Controllers`.
* Remove fully qualified base class references caused by namespace mismatch.
* Ensure DTOs, services, middleware, and hubs follow the same convention.
* Keep namespaces consistent with folders.

### Result

Imports are cleaner and code search becomes more reliable.

---

## Phase 5. Clean Encoding and Comments

### Goal

Remove mojibake and stale comments.

### Tasks

* Re-save corrupted files as UTF-8.
* Replace unreadable comments with concise Ukrainian or English comments.
* Remove comments that only describe obvious code.
* Replace temporary comments like `TODO` with tracked tasks or implementations.
* Avoid mixed-language comments inside one method when possible.

### Result

The codebase becomes easier to read without changing behavior.

---

## Phase 6. Add Verification Gates

### Goal

Catch regressions during refactoring.

### Tasks

* Keep `dotnet build uchat.sln --no-restore` green.
* Add server tests for tenant context lifetime, auth, permissions, and file path validation.
* Add migration checks before database refactors.
* Add lightweight client tests around extracted logic where possible.
* Track known package warnings separately from new warnings.

### Result

Refactoring can proceed in small, verifiable steps.

