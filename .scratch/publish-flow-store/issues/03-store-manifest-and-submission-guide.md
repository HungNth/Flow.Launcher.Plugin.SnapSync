# Issue 03: Store Manifest and Submission Guide

**Status:** resolved
**Parent Specification:** `.scratch/publish-flow-store/spec.md`

## Required Behavior

Prepare the store submission assets and human guide:
1. Generate the store submission manifest file ready to be copied or PR'd into `Flow-Launcher/Flow.Launcher.PluginsManifest`:
   - Path: `plugins/SnapSync-E8332E900BD54E2C854DC406B78B1143.json`
   - Fields:
     - `ID`: `"E8332E900BD54E2C854DC406B78B1143"`
     - `Name`: `"SnapSync"`
     - `Description`: `"On-demand, one-way file and folder synchronization with safety checks and preview."`
     - `Author`: `"HungNth"`
     - `Version`: `"1.0.0"`
     - `Language`: `"csharp"`
     - `Website`: `"https://github.com/HungNth/Flow.Launcher.Plugin.SnapSync"`
     - `UrlDownload`: `"https://github.com/HungNth/Flow.Launcher.Plugin.SnapSync/releases/download/v1.0.0/Flow.Launcher.Plugin.SnapSync.zip"`
     - `UrlSourceCode`: `"https://github.com/HungNth/Flow.Launcher.Plugin.SnapSync/tree/main"`
     - `IcoPath`: `"https://cdn.jsdelivr.net/gh/HungNth/Flow.Launcher.Plugin.SnapSync@main/Images/app.png"`
     - `MinimumAppVersion`: `"2.0.0"`
2. Provide a clear documentation guide `docs/publishing-guide.md` covering:
   - Git push & tag trigger commands (`git tag v1.0.0`, `git push origin v1.0.0`).
   - Verifying the GitHub Actions release build and asset download.
   - Forking `Flow-Launcher/Flow.Launcher.PluginsManifest` and submitting the PR with the manifest file.
   - Future update workflow (automatic every 3 hours by Flow bot upon new tag).

## Acceptance Criteria

1. Store manifest JSON file is created and passes syntax/schema validation.
2. `docs/publishing-guide.md` contains accurate, sequential instructions.
3. No dummy or fabricated placeholder URLs are used.

## Testing Seam

- JSON schema verification against official Flow.Launcher.PluginsManifest records.
- Documentation review.

## Demo Path

Display the manifest contents and verified publishing instructions.

## Blockers / Dependencies

Blocked by Issue 02.
