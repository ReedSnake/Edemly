# Server File Storage

This document describes the current server-side file storage behavior.

The current implementation stores uploaded files on the local server filesystem under `wwwroot`. This is a pragmatic development-time storage model. The planned production direction is to move file storage to MinIO or another S3-compatible object storage service.

## Contents

* [Overview](#overview)
* [Current Implementation](#current-implementation)
* [Configuration](#configuration)
* [Storage Layout](#storage-layout)
* [Upload Endpoints](#upload-endpoints)
* [Download and Delete Behavior](#download-and-delete-behavior)
* [Tenant Behavior](#tenant-behavior)
* [Security Notes](#security-notes)
* [MinIO Migration Direction](#minio-migration-direction)
* [Current Limitations](#current-limitations)
* [Related Documents](#related-documents)

## Overview

File storage is currently handled by `FileStorageService` through the `IFileStorageService` interface.

The service is registered in `Program.cs`:

```csharp
builder.Services.AddScoped<IFileStorageService, FileStorageService>();
```

The implementation uses `IWebHostEnvironment.WebRootPath` and writes files under:

```text
Edemly.Server/wwwroot/uploads
```

If `WebRootPath` is not available, the service falls back to a `wwwroot` path under the current working directory for path construction.

## Current Implementation

The current storage implementation is local filesystem storage.

Main implementation files:

| File | Responsibility |
| ---- | -------------- |
| `Infrastructure/Files/IFileStorageService.cs` | Storage abstraction used by controllers |
| `Infrastructure/Files/FileStorageService.cs` | Local `wwwroot/uploads` implementation |
| `Configuration/FileStorageSettings.cs` | Storage-related configuration model |
| `Api/Controllers/Files/FileController.cs` | Generic file upload, download, and delete endpoints |
| `Api/Controllers/Users/UserController.cs` | Profile picture upload endpoint |
| `Api/Controllers/Chats/ChatFilesController.cs` | Group chat icon upload endpoint |

Uploaded files are copied from the request stream into local files. The service then returns a URL string that is stored or sent back to the client.

## Configuration

The current configuration lives under `FileStorage` in `Edemly.Server/appsettings.json`.

```json
"FileStorage": {
  "StoragePath": "uploads",
  "ProfilePicturesFolder": "profile-pictures",
  "FilesFolder": "files",
  "BaseUrl": "/uploads"
}
```

The settings model also contains:

| Setting | Default | Purpose |
| ------- | ------- | ------- |
| `StoragePath` | `uploads` | Folder under `wwwroot` |
| `ProfilePicturesFolder` | `profile-pictures` | Subfolder for profile pictures |
| `FilesFolder` | `files` | Subfolder for generic uploaded files and group icons |
| `MaxFileSizeMB` | `50` | Maximum file size enforced by `FileStorageService` |
| `BaseUrl` | `/uploads` | URL prefix returned by the storage service |

Upload controllers also use `[RequestSizeLimit(52428800)]`, so the effective request limit is currently 50 MB.

## Storage Layout

For personal or global context uploads, the layout is:

```text
wwwroot/
`-- uploads/
    |-- profile-pictures/
    `-- files/
```

For tenant/company context uploads, the service adds the current company name as a folder segment:

```text
wwwroot/
`-- uploads/
    `-- {company-name}/
        |-- profile-pictures/
        `-- files/
```

Profile picture file names use this pattern:

```text
user_{userId}_{yyyyMMdd_HHmmss}_{randomId}{extension}
```

Generic file uploads use this pattern:

```text
user_{userId}_{yyyyMMdd_HHmmss}_{safeOriginalName}{extension}
```

Group icons are uploaded through the generic file storage method with a generated `group_{chatId}_{ticks}{extension}` file name.

## Upload Endpoints

The current upload endpoints are:

| Method | Route | Auth | Purpose |
| ------ | ----- | ---- | ------- |
| `POST` | `api/files` | Required | Uploads a generic file |
| `POST` | `api/users/me/profile-picture` | Required | Uploads and saves the current user's profile picture |
| `POST` | `api/chats/{chatId}/icon` | Required | Uploads and saves a group chat icon |

Profile pictures and group icons currently allow only these extensions:

```text
.jpg, .jpeg, .png, .gif
```

Generic file upload does not currently enforce the same extension allowlist. It stores the request content type and original file name in the upload response.

## Download and Delete Behavior

Generic files can be downloaded through:

```text
GET api/files/download?fileUrl={fileUrl}
```

This endpoint is currently marked with `[AllowAnonymous]`. It resolves the stored URL through `FileStorageService.GetFileAsync` and returns the file stream with a content type inferred from the file extension.

Generic files can be deleted through:

```text
DELETE api/files?fileUrl={fileUrl}
```

Delete requires authentication. The current service maps the URL back to a local relative path and deletes the matching file from `wwwroot/uploads`.

## Tenant Behavior

When a tenant is resolved, uploaded files are placed under that tenant's company folder inside `uploads`.

Example:

```text
wwwroot/uploads/acme/profile-pictures/user_4_20260612_180000_ab12cd34.png
```

The service includes a guard that prevents the master/global context from reading or deleting tenant files through `FileStorageService` if the first path segment matches a known company name.

Tenant file isolation should be reviewed again during the MinIO migration. Object storage should use explicit bucket/key rules rather than relying on path segments alone.

## Security Notes

Current local storage is suitable for development and local testing, but it is not a final production storage model.

Important current behavior:

* `app.UseStaticFiles()` is enabled, so files under `wwwroot` can be served by ASP.NET Core static file middleware.
* `EnsureUploadsAuthMiddleware` exists and checks `/uploads` paths, but it is currently registered after `UseStaticFiles()`. Do not rely on it as the only access-control boundary for local uploaded files until middleware ordering and static-file access are reviewed.
* `api/files/download` is currently anonymous by controller attribute.
* Profile picture and group icon uploads validate file extensions, but generic file upload currently does not have a strict extension allowlist.
* Local uploaded files should not be committed to the repository.
* File URLs are currently persisted and passed around as strings. Changing storage backends should preserve a stable URL/key contract for existing messages and profiles.

## MinIO Migration Direction

The intended future storage model is MinIO or another S3-compatible object storage service.

The preferred migration path is:

1. Keep `IFileStorageService` as the application-facing storage boundary.
2. Add a MinIO-backed implementation, for example `MinioFileStorageService`.
3. Introduce storage provider configuration such as `FileStorage:Provider = Local | Minio`.
4. Store object keys separately from public URLs where possible.
5. Use buckets and object prefixes for global and tenant scopes.
6. Decide whether file access should use authenticated proxy endpoints or short-lived presigned URLs.
7. Keep existing local `wwwroot` storage available for development if useful.

Possible MinIO layout:

```text
bucket: edemly-files

global/profile-pictures/{file}
global/files/{file}
tenants/{company-name}/profile-pictures/{file}
tenants/{company-name}/files/{file}
```

The MinIO implementation should avoid leaking physical storage details into API contracts. Controllers should continue to depend on `IFileStorageService`.

## Current Limitations

The following areas should be improved before production use:

* Local files are tied to the server instance and are not shared across multiple app instances.
* Backups, lifecycle cleanup, retention rules, and orphan-file cleanup are not documented yet.
* Access control for direct `/uploads` static files needs a dedicated review.
* Generic file upload needs stricter validation if arbitrary file types are not intended.
* There is no virus scanning or content inspection.
* There is no quota model per user, chat, company, or tenant.
* The current URL-as-identifier model may need to become a stable storage-key model before or during the MinIO migration.

## Related Documents

* [Server API](API.md)
* [Server Architecture](ARCHITECTURE.md)
* [Server Authentication](AUTH.md)
* [Server Database](DATABASE.md)
