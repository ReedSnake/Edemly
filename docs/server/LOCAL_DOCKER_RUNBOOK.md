# Local Docker And Update Runbook

This runbook is the practical checklist for local testing of:

* Docker startup
* Static client bootstrap
* Installer download
* Optional updates
* Mandatory updates
* Publishing a new client release without restarting the static container
* Gateway fallback
* Future Redis scale-out planning

## Current Local Topology

```text
static nginx       http://localhost:8080
gateway           http://localhost:3500  primary API/payment gateway
gateway2          http://localhost:3600  backup API/payment gateway
hub-gateway       http://localhost:3700  SignalR gateway
server1           http://localhost:3501  backend debug port
mysql             localhost:3306
minio             localhost:9000
minio console     http://localhost:9001
```

`gateway`, `gateway2`, and `hub-gateway` all proxy to the same backend container, `server1`. This is intentional for now so SignalR messages, calls, and in-memory server state stay in one backend process.

## Start The Local Stack

From the repository root:

```powershell
docker compose -f deployment/local/docker-compose.yml up --build
```

In a second terminal, check the public endpoints:

```powershell
Invoke-WebRequest -UseBasicParsing http://localhost:8080/client.json
Invoke-WebRequest -UseBasicParsing http://localhost:8080/releases.json
Invoke-WebRequest -UseBasicParsing http://localhost:3500/health
Invoke-WebRequest -UseBasicParsing http://localhost:3600/health
Invoke-WebRequest -UseBasicParsing http://localhost:3700/gateway/health
```

The MinIO console is available at:

```text
http://localhost:9001
```

Default local credentials:

```text
edemly_admin / edemly_password
```

## Quick Client Test Without Installer

This path is for fast local debugging. It does not test Velopack installation or update installation.

```powershell
dotnet run --project Edemly.Client
```

With no argument, the client reads:

```text
http://localhost:8080/client.json
```

Direct server mode still works, but pass the hub endpoint explicitly:

```powershell
dotnet run --project Edemly.Client -- http://localhost:3500 --hub-server http://localhost:3500
```

## Build And Publish A Baseline Installer

Use this for the first installed client version, for example `1.0.0`.

```powershell
$version = "1.0.0"
$publishDir = ".artifacts\client\win-x64\$version"
$updateDir = "deployment\local\static\updates\windows\stable"

dotnet publish Edemly.Client\Edemly.Client.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:Version=$version `
  -o $publishDir

vpk pack `
  -u NSO.Edemly `
  -v $version `
  -p $publishDir `
  -e Edemly.exe `
  -o $updateDir `
  --packTitle Edemly
```

Expected files:

```text
deployment/local/static/updates/windows/stable/
|-- releases.win.json
|-- NSO.Edemly-win-Setup.exe
`-- NSO.Edemly-1.0.0-full.nupkg
```

These generated files are ignored by Git. The repository keeps only `.gitkeep` in this directory so the folder exists before the first local release is packed.

Set the baseline metadata in:

```text
deployment/local/static/client.json
deployment/local/static/releases.json
```

```json
"updates": {
  "windowsStableUrl": "http://localhost:8080/updates/windows/stable",
  "installerUrl": "http://localhost:8080/updates/windows/stable/NSO.Edemly-win-Setup.exe",
  "latestVersion": "1.0.0",
  "minimumRequiredVersion": "1.0.0",
  "mandatory": false
}
```

Open and install:

```text
http://localhost:8080/
http://localhost:8080/download/
http://localhost:8080/release/
http://localhost:8080/support/
```

The installed app should be named `Edemly`, and the main executable should be `Edemly.exe`.

## Preview The Static Site With VS Code Live Server

For visual checks only, you can open:

```text
deployment/local/static/index.html
```

with VS Code Live Server. The static pages use relative links, and `assets/app.js` resolves `client.json`, `releases.json`, and `/downloads/...` paths from the real static root. This lets the pages work when Live Server serves the whole repository instead of serving `deployment/local/static` as `/`.

Static page roles:

* `/` is the product home page for the messenger.
* `/download/` shows only published packages with download links for the selected platform.
* `/release/` shows full release history with pagination and dynamic details from `releases.json`.
* `/support/` links to the feedback form and support paths.

Use nginx at `http://localhost:8080/` for client updater testing. The WPF client still expects the configured `client.json` and Velopack feed URLs, and those should be validated through the local Docker static service.

The sample files under `deployment/local/static/downloads/windows/{version}/` can be empty placeholders for visual checks. Replace them with real installer and portable artifacts before testing actual downloads.

## Publish A New Version Without Restarting Docker

Keep Docker running. Do not stop `edemly-local-static`.

The `static` service uses a bind mount:

```yaml
./static:/usr/share/nginx/html:ro
```

That means files changed under `deployment/local/static` are served immediately by the already running nginx container.

Publish a new version into the same update directory:

```powershell
$version = "1.0.1"
$publishDir = ".artifacts\client\win-x64\$version"
$updateDir = "deployment\local\static\updates\windows\stable"

dotnet publish Edemly.Client\Edemly.Client.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:Version=$version `
  -o $publishDir

vpk pack `
  -u NSO.Edemly `
  -v $version `
  -p $publishDir `
  -e Edemly.exe `
  -o $updateDir `
  --packTitle Edemly
```

Check that nginx sees the new feed while the container is still running:

```powershell
Invoke-WebRequest -UseBasicParsing http://localhost:8080/updates/windows/stable/releases.win.json
```

The installed client checks updates on startup, so restart only the installed Edemly client after changing release files or `client.json`.

Also update `deployment/local/static/releases.json` so the download and release pages show the new version. The download page only shows releases that expose platform-specific download links. The release page keeps full history, including archived builds without public downloads.

For the planned local tool that will update both JSON files and copy release files, see `docs/server/LOCAL_RELEASE_MANAGER_PLAN.md`.

## Test Optional Update

After publishing `1.0.1`, set:

```json
"latestVersion": "1.0.1",
"minimumRequiredVersion": "1.0.0",
"mandatory": false
```

Restart the installed Edemly client.

Expected behavior:

* The main window opens.
* The top bar shows an available update.
* `Update now` starts download and install.
* `Remind later` hides the bar until the next client start.
* `X` hides the bar until the next client start.

## Test Mandatory Update

Option A, require all clients older than `1.0.1` to update:

```json
"latestVersion": "1.0.1",
"minimumRequiredVersion": "1.0.1",
"mandatory": false
```

Option B, force the current release regardless of version comparison:

```json
"latestVersion": "1.0.1",
"minimumRequiredVersion": "1.0.0",
"mandatory": true
```

Restart the installed Edemly client.

Expected behavior:

* The main window opens.
* The top bar shows a required update.
* The client starts installing the update without allowing postpone or close.

## Test Gateway Fallback

This tests fallback between public gateway endpoints, not backend server failover.

Start the full stack:

```powershell
docker compose -f deployment/local/docker-compose.yml up --build
```

Stop the primary gateway only:

```powershell
docker stop edemly-local-gateway
```

Check expected endpoint state:

```powershell
Invoke-WebRequest -UseBasicParsing http://localhost:3600/health
Invoke-WebRequest -UseBasicParsing http://localhost:3700/gateway/health
```

`http://localhost:3500/health` should fail while the primary gateway is stopped.

Restart the installed client or run the client with no arguments:

```powershell
dotnet run --project Edemly.Client
```

Expected behavior:

* Static config is still read from `http://localhost:8080/client.json`.
* Health check for `local-primary` fails.
* Client selects `local-backup` through `http://localhost:3600`.
* Hubs still connect through `http://localhost:3700`.

Restore the primary gateway:

```powershell
docker start edemly-local-gateway
```

You can also stop the backup gateway:

```powershell
docker stop edemly-local-gateway2
docker start edemly-local-gateway2
```

If you stop `hub-gateway`, REST/API can still work through `3500` or `3600`, but realtime and calls should fail because `client.json` points hubs to `3700`:

```powershell
docker stop edemly-local-hub-gateway
docker start edemly-local-hub-gateway
```

If you stop `server1`, all gateways fail because the current local stack has one backend server:

```powershell
docker stop edemly-local-server1
docker start edemly-local-server1
```

Real backend server failover needs the Redis work described below.

## Redis Scale-Out Analysis

Do not add a second backend server just by duplicating `server1`. The current code has state that is local to one process.

Redis should be added when you want more than one backend server instance behind the gateways.

Recommended first use of Redis:

* SignalR backplane for cross-server `Clients.User(...)` and `Clients.Users(...)` delivery.
* Distributed presence state instead of the current in-memory `UserPresenceService`.
* Distributed verification code storage instead of static dictionaries in email services.
* Distributed cache or cache invalidation for message cache entries.
* Distributed lock for background workers so reminders and maintenance jobs are not processed by every server instance.

Important current in-memory areas:

* `UserPresenceService` stores online users and connection ids in `ConcurrentDictionary`.
* `EmailService` and `MockEmailService` store verification codes in static dictionaries.
* `ChatCacheRegistry` and `IMemoryCache` are process-local.
* `CallHub` schedules pending call timeout work with in-process `Task.Delay`.
* `ServerMaintenanceWorker` would run in every backend container if multiple server containers are started.

Suggested Redis implementation order:

1. Add a `redis` service to `deployment/local/docker-compose.yml`.
2. Add `Microsoft.AspNetCore.SignalR.StackExchangeRedis` to `Edemly.Server`.
3. Configure SignalR to call `AddStackExchangeRedis` only when `Redis:ConnectionString` is set.
4. Add `server2` only after the SignalR backplane is enabled.
5. Move presence and verification code storage behind small services that can use Redis.
6. Add a distributed lock for `ServerMaintenanceWorker`.
7. Revisit call timeout scheduling so a backend crash does not lose pending call timeout work.
8. Only then change gateway clusters from a single `server1` destination to `server1` plus `server2`.

Draft Docker shape:

```yaml
redis:
  image: redis:7-alpine
  ports:
    - "6379:6379"
  healthcheck:
    test: ["CMD", "redis-cli", "ping"]
    interval: 10s
    timeout: 5s
    retries: 6
```

Server environment:

```yaml
Redis__ConnectionString: redis:6379
SignalR__Backplane: Redis
```

Gateway routing after Redis:

* API routes can use `RoundRobin` across `server1` and `server2`.
* Main chat hub can use Redis backplane across `server1` and `server2`.
* Call hub should be tested carefully because audio chunks may create heavy Redis traffic. If audio relay becomes unstable, keep `/call` on a dedicated sticky hub route while scaling REST and normal chat first.

For the current local testing goal, keep one backend server and multiple gateways. This tests installer/update/static/gateway fallback without mixing in Redis scale-out risk.
