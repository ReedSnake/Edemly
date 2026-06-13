# Deployment

This document describes the current local deployment profile. It is intended for local end-to-end testing of the server, gateways, static bootstrap site, MinIO uploads, and client update feed.

## Local Stack

The local stack is defined in:

```text
deployment/local/docker-compose.yml
```

Start everything:

```powershell
docker compose -f deployment/local/docker-compose.yml up --build
```

Start only infrastructure and the static site:

```powershell
docker compose -f deployment/local/docker-compose.yml up mysql minio minio-init static
```

## Services

| Service | Port | Notes |
| ------- | ---- | ----- |
| `mysql` | `3306` | Main MySQL database. |
| `minio` | `9000` | S3-compatible upload storage. |
| `minio` console | `9001` | Local MinIO console. |
| `static` | `8080` | Client bootstrap config, download page, and Velopack update feed. |
| `server1` | `3501` | Internal backend instance exposed for debugging. |
| `gateway` | `3500` | Primary public API, upload, payment, and SignalR gateway. |
| `gateway2` | `3600` | Backup gateway for client fallback testing. |
| `hub-gateway` | `3700` | Dedicated public SignalR gateway for `/main` and `/call`. |

Default local MinIO credentials:

```text
edemly_admin / edemly_password
```

The compose profile creates the `edemly-uploads` bucket through `minio-init`.

## Client Bootstrap

The client can start without a server argument when the static site is running. By default it reads:

```text
http://localhost:8080/client.json
```

The local bootstrap file points to:

```text
API/payment: http://localhost:3500
API/payment fallback: http://localhost:3600
hubs: http://localhost:3700
```

The client checks `/health` on each enabled server and selects the first healthy endpoint by priority.

## Gateway Routing

The gateway routes:

| Route | Upstream |
| ----- | -------- |
| `/main` and `/call` | SignalR hubs on `server1`. |
| `/api/payment` | Payment endpoints on `server1`. |
| `/api` | REST API on `server1`. |
| `/uploads` | Authenticated upload downloads on `server1`. |
| `/health` | Backend health check on `server1`. |
| `/gateway/health` | Gateway process health check. |

The local client config uses the dedicated `hub-gateway` endpoint for SignalR while keeping API/payment fallback separate. All gateways still proxy to the same `server1` backend so SignalR events remain in one server process.

## Static Updates

Local static update files live under:

```text
deployment/local/static/updates/windows/stable/
```

Expected Velopack output:

```text
releases.win.json
NSO.Edemly-win-Setup.exe
NSO.Edemly-1.0.0-full.nupkg
```

The generated installer and package files are ignored by Git. Commit only placeholders and documentation.

For release creation, version fields, mandatory update policy, and local installer testing, see [Client Releases and Updates](RELEASES.md).

For a full manual local test sequence, including hot-publishing without restarting Docker, gateway fallback, and Redis scale-out notes, see [Local Docker And Update Runbook](LOCAL_DOCKER_RUNBOOK.md).

## Not Done

The following deployment work still needs a real environment or release artifacts:

* Validate `docker compose -f deployment/local/docker-compose.yml config` and `up --build` on a machine with Docker Desktop.
* Generate real Velopack Windows artifacts and replace the placeholder `releases.win.json`.
* Decide production static URLs and replace local `localhost` bootstrap/update URLs.
* Add production secrets handling for database, JWT, Brevo, MinIO, and WayForPay.
* Add CI/CD publishing for Docker images and Velopack artifacts.
