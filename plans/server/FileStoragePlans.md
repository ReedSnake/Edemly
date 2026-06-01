# Edemly File Storage Roadmap

## Phase 1. Fix Upload Authorization Flow

### Goal

Ensure uploaded files are protected when they should be protected.

### Current Issue

Static files are served before `EnsureUploadsAuthMiddleware`, so the middleware may never run for upload paths.

### Tasks

* Move upload authorization before static file serving or replace direct static serving with an authenticated file endpoint.
* Decide which files are public: avatars, group icons, attachments, voice messages.
* Do not mark downloads `[AllowAnonymous]` unless the file category is explicitly public.
* Add tests for anonymous access to `/uploads/...` and `/{tenant}/uploads/...`.

### Result

Files are not exposed accidentally through static middleware ordering.

---

## Phase 2. Prevent Path Traversal

### Goal

Ensure user-controlled file URLs cannot escape the uploads directory.

### Tasks

* Canonicalize paths with `Path.GetFullPath`.
* Verify every resolved path starts inside the expected uploads root.
* Reject absolute paths, drive-qualified paths, and `..` segments.
* Store file metadata in the database instead of trusting arbitrary `fileUrl` from the client.
* Use file ids for delete/download operations where possible.

### Result

Delete and download operations cannot access files outside managed storage.

---

## Phase 3. Improve Filename Sanitization

### Goal

Make stored filenames safe and portable.

### Tasks

* Replace current partial cleanup with a strict allowlist for filename characters.
* Preserve original filename separately as metadata.
* Generate storage names from GUIDs instead of user filenames.
* Validate and normalize file extensions.
* Validate content type by inspecting file signatures for high-risk file categories.

### Result

Uploaded files have safe storage names while users can still see original names.

---

## Phase 4. Add File Ownership and Tenant Metadata

### Goal

Make file access decisions based on stored ownership, not path guessing.

### Database

```text
UploadedFile
------------
Id
TenantId
OwnerUserId
StoragePath
OriginalFileName
ContentType
Size
Category
CreatedAt
```

### Tasks

* Add file metadata table.
* Save owner, tenant, category, and storage path during upload.
* Check owner/chat membership before download or delete.
* Keep tenant files separated by tenant id or tenant database.
* Add cleanup for orphaned files.

### Result

File access is enforceable and auditable.

---

## Phase 5. Add MinIO / Object Storage

### Goal

Store production files outside the application directory.

### Storage Options

```text
Local development:
uchat_server/wwwroot/uploads

Production:
MinIO or another S3-compatible object storage
```

### Tasks

* Keep local filesystem storage for development.
* Add a MinIO/S3 implementation behind `IFileStorageService`.
* Configure endpoint, bucket, access key, secret key, region, and public/private URL mode through appsettings or environment variables.
* Create buckets during deployment or startup only when explicitly enabled.
* Store object keys in the database instead of local relative paths.
* Support tenant-aware object keys, for example `tenants/{tenant}/files/{fileId}`.
* Decide whether downloads are proxied through the API or served through short-lived signed URLs.
* Add retry and timeout handling for object storage operations.
* Document local vs production file storage behavior.

### Result

Production uploads are stored in MinIO/S3-compatible storage instead of `wwwroot`.

---

## Phase 6. Git Hygiene for Uploads

### Goal

Keep local uploaded files out of source control.

### Tasks

* Add `uchat_server/wwwroot/uploads/` to `.gitignore`.
* Keep placeholder directories only if needed, using `.gitkeep`.
* Avoid committing generated user files.
* Document local storage behavior in setup docs.

### Result

Runtime uploads do not appear as untracked project files.
