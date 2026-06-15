# Client Releases and Updates

This document describes the local release flow for the Edemly Windows client and the static update site.

## Runtime Behavior

The client checks for updates once during startup after the main window is shown.

Optional updates are shown in the top status bar of `MainWindow`. The bar contains:

* `Update now`
* `Remind later`
* `X`

`Remind later` and `X` hide the update bar until the next client process start. They do not write a persistent setting.

Mandatory updates are controlled by static config and are installed without user confirmation. The client still shows the top bar while it starts the download/apply flow, but it does not allow postponing the update.

The update check is skipped when the app is not installed by Velopack. This keeps quick `dotnet run` testing independent from installer artifacts.

## Static Config

Local static config lives at:

```text
deployment/local/static/client.json
```

Important fields:

```json
{
  "servers": [
    {
      "apiBaseUrl": "http://localhost:3500",
      "hubBaseUrl": "http://localhost:3700",
      "paymentBaseUrl": "http://localhost:3500"
    }
  ],
  "updates": {
    "windowsStableUrl": "http://localhost:8080/updates/windows/stable",
    "installerUrl": "http://localhost:8080/updates/windows/stable/NSO.Edemly-win-Setup.exe",
    "latestVersion": "1.0.0",
    "minimumRequiredVersion": "1.0.0",
    "mandatory": false
  }
}
```

Use `apiBaseUrl` for REST, uploads, and payments. Use `hubBaseUrl` for `/main` and `/call` SignalR connections. The local Docker profile exposes a dedicated hub gateway at `http://localhost:3700`, backed by the same `server1` instance so realtime events stay in one process.

## Server Endpoint Boundaries

The static client config separates server endpoints by responsibility:

| Field | Used for |
| ----- | -------- |
| `apiBaseUrl` | REST APIs, authenticated upload reads, and general server health checks |
| `hubBaseUrl` | `/main` and `/call` SignalR hub connections |
| `paymentBaseUrl` | payment form and payment return/status routes |

Local gateway fallback covers public gateway availability. It does not provide backend failover because the current local profile still routes every gateway to the same `server1` process.

Uploaded files are served through authenticated server paths under `/uploads/...`; the client does not need a direct MinIO URL.

## Version Rules

Update the version in two places for a real release:

* `Edemly.Client/Edemly.Client.csproj` -> `<Version>`
* `deployment/local/static/client.json` -> `updates.latestVersion`

Set `updates.minimumRequiredVersion` to the oldest client version that is still allowed to run. If the installed app version is lower than `minimumRequiredVersion`, the client treats the update as mandatory.

Set `updates.mandatory` to `true` only when every installed client should update immediately, regardless of `minimumRequiredVersion`.

## Build A Windows Release

From the repository root:

```powershell
$version = "1.0.0"
$publishDir = ".artifacts\client\win-x64"
$updateDir = "deployment\local\static\updates\windows\stable"

dotnet publish Edemly.Client\Edemly.Client.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -o $publishDir
```

Confirm the publish output contains:

```text
.artifacts/client/win-x64/Edemly.exe
```

Pack the Velopack release:

```powershell
vpk pack `
  -u NSO.Edemly `
  -v $version `
  -p $publishDir `
  -e Edemly.exe `
  -o $updateDir `
  --packTitle Edemly
```

If your installed Velopack CLI has different option names, run `vpk pack --help` and keep the same values:

* package id: `NSO.Edemly`
* version: current release version
* package directory: published client output
* main executable: `Edemly.exe`
* title: `Edemly`
* output directory: `deployment/local/static/updates/windows/stable`

Expected local output:

```text
deployment/local/static/updates/windows/stable/
|-- releases.win.json
|-- NSO.Edemly-win-Setup.exe
`-- NSO.Edemly-1.0.0-full.nupkg
```

Installer, package, and generated release feed artifacts are ignored by Git. Commit the updated docs, `client.json`, and version metadata, not generated binaries.

## Test Locally

Host only the static site:

```powershell
docker compose -f deployment/local/docker-compose.yml up static
```

Open:

```text
http://localhost:8080/download/
```

Install Edemly from the download page, then start the installed app. `dotnet run` is still valid for quick server/client testing, but it will not exercise Velopack update installation.

Host the full local stack:

```powershell
docker compose -f deployment/local/docker-compose.yml up --build
```

Relevant local URLs:

```text
http://localhost:8080  static site and updates
http://localhost:3500  primary API/payment gateway
http://localhost:3600  backup API/payment gateway
http://localhost:3700  hub gateway
```

## Publish Without Restarting Docker

The local `static` service uses a bind mount:

```yaml
./static:/usr/share/nginx/html:ro
```

Because of that, nginx can keep running while you replace files on the host under:

```text
deployment/local/static/
```

You do not need to restart the Docker container after publishing a new client release. Generate or copy the new Velopack output directly into:

```text
deployment/local/static/updates/windows/stable/
```

Then update and save:

```text
deployment/local/static/client.json
```

The already running static container will serve the new installer, feed, and config immediately. Running clients check for updates only on startup, so restart the installed Edemly client to test a newly published update.

## End-To-End Local Update Test

Use this flow to test static hosting, Docker addresses, installer download, and Velopack update behavior.

1. Start the local stack and keep it running:

```powershell
docker compose -f deployment/local/docker-compose.yml up --build
```

2. Build and publish the baseline version, for example `1.0.0`:

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

3. Set `deployment/local/static/client.json` update metadata to the same baseline version:

```json
"updates": {
  "windowsStableUrl": "http://localhost:8080/updates/windows/stable",
  "installerUrl": "http://localhost:8080/updates/windows/stable/NSO.Edemly-win-Setup.exe",
  "latestVersion": "1.0.0",
  "minimumRequiredVersion": "1.0.0",
  "mandatory": false
}
```

4. Open the download page and install Edemly:

```text
http://localhost:8080/download/
```

5. Without stopping Docker, publish a newer version, for example `1.0.1`, into the same update directory:

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

6. For an optional update, set:

```json
"latestVersion": "1.0.1",
"minimumRequiredVersion": "1.0.0",
"mandatory": false
```

Start the installed Edemly client. It should show the update bar with `Update now`, `Remind later`, and `X`.

7. For a mandatory update, set either:

```json
"latestVersion": "1.0.1",
"minimumRequiredVersion": "1.0.1",
"mandatory": false
```

or:

```json
"latestVersion": "1.0.1",
"minimumRequiredVersion": "1.0.0",
"mandatory": true
```

Start the installed Edemly client. It should show the update bar and then install without allowing postpone/close.

8. Check the live static feed while Docker is still running:

```powershell
Invoke-WebRequest -UseBasicParsing http://localhost:8080/client.json
Invoke-WebRequest -UseBasicParsing http://localhost:8080/updates/windows/stable/releases.win.json
```

## Change Addresses

For another environment, update only `deployment/local/static/client.json` or the equivalent hosted static config:

* `servers[].apiBaseUrl`
* `servers[].hubBaseUrl`
* `servers[].paymentBaseUrl`
* `updates.windowsStableUrl`
* `updates.installerUrl`

For Docker payment callbacks, also update:

* `PublicBaseUrl`
* `WayForPay__DomainName`
* `WayForPay__ReturnUrl`

These values currently live in `deployment/local/docker-compose.yml` for local testing.
