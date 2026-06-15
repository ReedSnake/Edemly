# Server File Storage

This document describes the current server-side file storage behavior.

The current `FileStorageService` supports local filesystem storage and MinIO-backed storage behind the same `IFileStorageService` interface. Local development can use `wwwroot/uploads`; the local Docker profile is configured for MinIO.

## Contents

* [Overview](#overview)
* [Current Implementation](#current-implementation)
* [Configuration](#configuration)
* [Storage Layout](#storage-layout)
* [Upload Endpoints](#upload-endpoints)
* [Download and Delete Behavior](#download-and-delete-behavior)
* [Tenant Behavior](#tenant-behavior)
* [Security Notes](#security-notes)
* [MinIO Behavior](#minio-behavior)
* [Current Limitations](#current-limitations)
* [Related Documents](#related-documents)

## Overview

File storage is handled by `FileStorageService` through the `IFileStorageService` interface.

The service is registered in `Program.cs`:

```csharp
builder.Services.AddScoped<IFileStorageService, FileStorageService>();
```

When `FileStorage:Provider` is `Local`, the implementation uses `IWebHostEnvironment.WebRootPath` and writes files under:

```text
Edemly.Server/wwwroot/uploads
```

If `WebRootPath` is not available, the service falls back to a `wwwroot` path under the current working directory for path construction.

When `FileStorage:Provider` is `Minio`, the implementation writes objects to the configured MinIO bucket and still returns stable `/uploads/...` URLs for the client.

## Current Implementation

The current storage implementation is a provider switch inside `FileStorageService`.

Main implementation files:

| File | Responsibility |
| ---- | -------------- |
| `Infrastructure/Files/IFileStorageService.cs` | Storage abstraction used by controllers |
| `Infrastructure/Files/FileStorageService.cs` | Local and MinIO-backed implementation |
| `Configuration/FileStorageSettings.cs` | Storage-related configuration model |
| `Api/Controllers/Files/FileController.cs` | Generic file upload, download, and delete endpoints |
| `Api/Controllers/Users/UserController.cs` | Profile picture upload endpoint |
| `Api/Controllers/Chats/ChatFilesController.cs` | Group chat icon upload endpoint |

Uploaded files are copied from the request stream into the active storage provider. The service then returns a URL string that is stored or sent back to the client.

`Program.cs` registers `IMinioClient` only when `FileStorage:Provider` is `Minio`, then registers `IFileStorageService` as `FileStorageService`.

## Configuration

The current configuration lives under `FileStorage` in `Edemly.Server/appsettings.json`.

```json
"FileStorage": {
  "Provider": "Minio",
  "StoragePath": "uploads",
  "ProfilePicturesFolder": "profile-pictures",
  "FilesFolder": "files",
  "BaseUrl": "/uploads",
  "Minio": {
    "Endpoint": "localhost:9000",
    "AccessKey": "edemly_admin",
    "SecretKey": "edemly_password",
    "BucketName": "edemly-uploads",
    "ObjectPrefix": "",
    "Secure": false,
    "AutoCreateBucket": true
  }
}
```

The settings model also contains:

| Setting | Default | Purpose |
| ------- | ------- | ------- |
| `Provider` | `Local` | `Local` or `Minio` |
| `StoragePath` | `uploads` | Folder under `wwwroot` |
| `ProfilePicturesFolder` | `profile-pictures` | Subfolder for profile pictures |
| `FilesFolder` | `files` | Subfolder for generic uploaded files and group icons |
| `MaxFileSizeMB` | `50` | Maximum file size enforced by `FileStorageService` |
| `BaseUrl` | `/uploads` | URL prefix returned by the storage service |
| `Minio.Endpoint` | `localhost:9000` | MinIO API endpoint |
| `Minio.AccessKey` | empty | MinIO access key |
| `Minio.SecretKey` | empty | MinIO secret key |
| `Minio.BucketName` | `edemly-uploads` | Object bucket |
| `Minio.ObjectPrefix` | empty | Optional object key prefix |
| `Minio.Secure` | `false` | Whether the MinIO client uses HTTPS |
| `Minio.AutoCreateBucket` | `true` | Whether the service creates the bucket when needed |

Upload controllers also use `[RequestSizeLimit(52428800)]`, so the effective request limit is currently 50 MB.

When `Provider` is `Minio`, `Program.cs` also accepts these environment fallbacks:

```text
MINIO_ENDPOINT
MINIO_ACCESS_KEY
MINIO_SECRET_KEY
MINIO_SECURE
```

## Storage Layout

For local personal or global context uploads, the layout is:

```text
wwwroot/
`-- uploads/
    |-- profile-pictures/
    `-- files/
```

For local tenant/company context uploads, the service adds the current company name as a folder segment:

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

For MinIO, the same relative path is used as the object name, optionally prefixed by `Minio.ObjectPrefix`.

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

This endpoint requires authentication. It resolves the stored URL through `FileStorageService.GetFileAsync` and returns the file stream with a content type inferred from the file extension.

Uploaded files can also be read through authenticated upload paths:

```text
GET /uploads/{**filePath}
GET /{company}/uploads/{**filePath}
```

Generic files can be deleted through:

```text
DELETE api/files?fileUrl={fileUrl}
```

Delete requires authentication. The service maps the URL back to a relative storage path and deletes the matching local file or MinIO object.

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

* `EnsureUploadsAuthMiddleware` checks `/uploads` paths and is registered before `UseStaticFiles()`.
* `api/files/download` and upload proxy paths require authentication.
* Profile picture and group icon uploads validate file extensions, but generic file upload currently does not have a strict extension allowlist.
* `FileStorageService` guards tenant-prefixed paths, but there is no per-file ownership or ACL table yet.
* Local uploaded files should not be committed to the repository.
* File URLs are currently persisted and passed around as strings. Changing storage backends should preserve a stable URL/key contract for existing messages and profiles.

## MinIO Behavior

MinIO support is implemented inside `FileStorageService`.

When `FileStorage:Provider` is `Minio`:

1. `Program.cs` registers `IMinioClient`.
2. Uploads are written with `PutObjectAsync`.
3. Reads use `GetObjectAsync` and return a stream through the authenticated server endpoint.
4. Deletes use `RemoveObjectAsync`.
5. The bucket is created automatically if `Minio.AutoCreateBucket` is enabled.

Typical MinIO object layout:

```text
bucket: edemly-files

global/profile-pictures/{file}
global/files/{file}
tenants/{company-name}/profile-pictures/{file}
tenants/{company-name}/files/{file}
```

The current local Docker profile uses:

```text
bucket: edemly-uploads
endpoint: minio:9000
```

The implementation avoids exposing MinIO object URLs directly. Controllers continue to depend on `IFileStorageService`, and clients continue to use returned `/uploads/...` style URLs.

## Current Limitations

The following areas should be improved before production use:

* Local files are tied to the server instance and are not shared across multiple app instances.
* Backups, lifecycle cleanup, retention rules, and orphan-file cleanup are not documented yet.
* File ownership and per-attachment ACL rules are not modeled in the database yet.
* Generic file upload needs stricter validation if arbitrary file types are not intended.
* There is no virus scanning or content inspection.
* There is no quota model per user, chat, company, or tenant.
* The current URL-as-identifier model may need to become a stable storage-key model before or during the MinIO migration.

## Related Documents

* [Server API](API.md)
* [Server Architecture](ARCHITECTURE.md)
* [Server Authentication](AUTH.md)
* [Server Security](SECURITY.md)
* [Server Database](DATABASE.md)
