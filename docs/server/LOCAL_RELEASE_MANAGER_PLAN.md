# Local Release Manager Plan

This plan keeps the public static site simple and lets a separate local tool update release metadata and files while nginx keeps running.

## Current Boundary

The static site reads:

* `deployment/local/static/client.json` for client runtime configuration and update policy.
* `deployment/local/static/releases.json` for product copy, platform metadata, system requirements, release history, media, support links, and download links.

The WPF client should not read `releases.json`. It continues to use `client.json` and the Velopack feed under `updates/windows/stable`.

## First Tool Shape

Start with a local file-backed tool instead of a database-backed admin service.

Recommended command shape:

```powershell
edemly-release-manager new `
  --version 1.0.3 `
  --date 2026-06-14 `
  --channel stable `
  --title "Edemly 1.0.3" `
  --summary "Small fixes and stability improvements." `
  --change "Fixed update loading issues." `
  --change "Improved chat startup behavior." `
  --installer ".artifacts\release\NSO.Edemly-win-Setup.exe" `
  --portable ".artifacts\release\NSO.Edemly-win-Portable.zip" `
  --full-package ".artifacts\release\NSO.Edemly-1.0.3-full.nupkg"
```

The tool should:

1. Validate the version and make sure it is newer than the current latest release.
2. Copy installer and portable files to `deployment/local/static/downloads/windows/{version}/`.
3. Publish Velopack feed files to `deployment/local/static/updates/windows/stable/`.
4. Update `deployment/local/static/releases.json`.
5. Update `deployment/local/static/client.json` with `updates.latestVersion`.
6. Update `updates.minimumRequiredVersion` only when the new release starts a new mandatory boundary.
7. Add platform-specific downloads only for packages that were actually published.
8. Attach screenshot or video URLs when release media is available.
9. Keep JSON formatting stable.
10. Leave generated release binaries ignored by Git.

Because `deployment/local/static` is bind-mounted into nginx, these changes are served immediately without restarting `edemly-local-static`.

For static-page previews, the manager can also create zero-byte placeholder files with the final installer and portable names. That is only for checking links and layout. Real release testing needs real artifacts.

## Download And Archive Rules

`releases.json` stores the full history. The download page does not list every supported release. It lists only releases with actual platform-specific download links:

```json
"downloads": {
  "windows": {
    "installer": "/downloads/windows/1.0.3/NSO.Edemly-win-Setup.exe",
    "portable": "/downloads/windows/1.0.3/NSO.Edemly-win-Portable.zip"
  }
}
```

The release page keeps archived and older releases visible. The static site can still use the latest release marked `"mandatory": true` as the supported-window boundary, but that boundary is a catalog rule, not page copy.

The manager should set `downloads` to `null` for archived releases outside the supported window. It may keep old files on disk temporarily, but the site should not link to them.

## Platform And Media Fields

Keep platform data in the catalog even before every platform has packages:

* `platforms` controls visible platform tabs.
* `systemRequirements` stores fake or real requirements per platform.
* release-level `platforms` records whether a version is available, planned, supported, or archived for each platform.
* release-level `media` can point to screenshots or short videos used on the release detail page.

## No Admin Endpoint Yet

Do not add `/admin/releases` yet. That would require authentication, upload limits, authorization, and audit behavior. A local tool avoids that risk for the current stage.

## Optional Later SQLite Mode

If editing metadata grows beyond a small CLI, add SQLite inside the local manager only:

* SQLite stores drafts, notes, channels, file checksums, and release metadata.
* The static site still reads exported JSON files, not SQLite directly.
* The manager writes `releases.json` and `client.json` after every publish.
* A future admin UI can sit on top of the local manager database without changing the static site contract.

This keeps the deployment path stable: static nginx serves files, the WPF client reads `client.json`, and the public release pages read `releases.json`.
