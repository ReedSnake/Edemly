# Edemly Operations and Reliability Roadmap

## Phase 1. Health Checks

### Goal

Make server health visible before users report failures.

### Tasks

* Add health check endpoints.
* Check master database connectivity.
* Check tenant database connectivity where practical.
* Check file storage availability.
* Check email provider mode and availability.
* Add a simple status view for admins later.

### Result

Deployment and runtime problems are easier to detect.

---

## Phase 2. Structured Logging

### Goal

Make logs useful for debugging real issues.

### Tasks

* Standardize log levels.
* Add correlation/request ids.
* Include tenant id/company in relevant logs.
* Avoid logging verification codes, secrets, tokens, and private message content.
* Reduce noisy information logs in hot paths.

### Result

Logs explain what happened without leaking sensitive data.

---

## Phase 3. Audit Logs

### Goal

Record security and administration actions.

### Events

* Login.
* Logout.
* Session revoked.
* User banned/unbanned.
* IP blocked/unblocked.
* Group invite created/used/revoked.
* Group member kicked/banned/promoted.
* File deleted.
* Admin settings changed.

### Tasks

* Add audit log entity.
* Store actor, target, tenant, action, timestamp, and metadata.
* Add admin search/filter for audit logs later.
* Protect audit logs from normal users.

### Result

Admin and security decisions are traceable.

---

## Phase 4. Backup and Restore

### Goal

Protect user and company data from accidental loss.

### Tasks

* Define backup strategy for master database.
* Define backup strategy for tenant databases.
* Define backup strategy for MinIO/S3 objects.
* Document restore steps.
* Test restoring into a local or staging environment.
* Consider retention policy and encryption.

### Result

Data can be recovered after failures or mistakes.

---

## Phase 5. Deployment and Release Safety

### Goal

Make production updates less risky.

### Tasks

* Separate development, staging, and production config.
* Move secrets to environment variables or secret storage.
* Add migration checklist before release.
* Add rollback strategy.
* Add version endpoint for the client updater.
* Keep release notes for client and server versions.

### Result

Server and client updates become controlled and reversible.

---

## Phase 6. Error Reporting

### Goal

Collect client and server failures in a diagnosable way.

### Tasks

* Add server-side centralized exception handling.
* Add client-side error reporting for crashes and failed operations.
* Include app version, OS version, tenant, and correlation id where safe.
* Avoid uploading private content in error reports.
* Add a local client log export option for support.

### Result

Production bugs become easier to reproduce and fix.

