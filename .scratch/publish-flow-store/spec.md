# Flow Launcher Plugin Store Publication & Release Automation

**Status:** ready-for-agent

## Problem Statement

SnapSync is implemented and verified locally, but it cannot be installed by the Flow Launcher community because:
1. `plugin.json` contains generic/placeholder metadata (`Author: "SnapSync"`, `Description: "Flow Launcher plugin for SnapSync"`).
2. The repository lacks automated CI/CD to build, package, and publish releases on GitHub as required by Flow Launcher Store policy.
3. Flow Launcher package 5.3.2 brings in host-bundled dependencies (`Microsoft.Windows.SDK.NET.dll` ~25MB) which needlessly bloat release archives unless intentionally excluded.
4. No official manifest file exists for the `Flow-Launcher/Flow.Launcher.PluginsManifest` directory.

## Solution

Deliver the end-to-end publishing pipeline for SnapSync:
1. Update [plugin.json](file:///F:/Apps/flow-launcher-plugins/Flow.Launcher.Plugin.SnapSync/plugin.json) with production metadata:
   - `Author: "HungNth"`
   - `Description: "On-demand, one-way file and folder synchronization with safety checks and preview."`
2. Create GitHub Actions workflow `.github/workflows/publish.yml`:
   - Trigger on push of tags matching `v*.*.*` and `workflow_dispatch`.
   - Setup .NET 9 SDK on `windows-latest`.
   - Run tests (`dotnet test`).
   - Publish `win-x64 --no-self-contained`.
   - Exclude host-provided assemblies (`Microsoft.Windows.SDK.NET.dll`, `Flow.Launcher.Plugin.dll`) per ADR 0005.
   - Compress to `Flow.Launcher.Plugin.SnapSync.zip`.
   - Create GitHub Release and attach the zip asset.
3. Provide the store submission manifest file ready for PR to `Flow-Launcher/Flow.Launcher.PluginsManifest`:
   - Path: `plugins/SnapSync-E8332E900BD54E2C854DC406B78B1143.json`
   - Fields: `ID`, `Name`, `Description`, `Author`, `Version`, `Language`, `Website`, `UrlDownload`, `UrlSourceCode`, `IcoPath` (jsDelivr CDN), `MinimumAppVersion: "2.0.0"`.
4. Provide step-by-step submission guide for publishing the first release and opening the PR.

## Acceptance Criteria

1. `plugin.json` specifies `"Author": "HungNth"` and `"Description": "On-demand, one-way file and folder synchronization with safety checks and preview."`.
2. `.github/workflows/publish.yml` passes syntax verification, builds for `win-x64 --no-self-contained`, runs tests, packages `Flow.Launcher.Plugin.SnapSync.zip` excluding host DLLs, and creates a GitHub release on `v*` tag.
3. Packaged zip size is < 1 MB (~250 KB) and contains `plugin.json`, `Flow.Launcher.Plugin.SnapSync.dll`, `SnapSync.Core.dll`, and `Images/app.png`.
4. `plugins/SnapSync-E8332E900BD54E2C854DC406B78B1143.json` matches the official `Flow.Launcher.PluginsManifest` schema and contains non-placeholder URLs.
5. All local tests continue to pass (`dotnet test`).
