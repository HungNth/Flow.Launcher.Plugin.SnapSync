# Issue 02: GitHub Actions Automated Release Workflow

**Status:** resolved
**Parent Specification:** `.scratch/publish-flow-store/spec.md`

## Required Behavior

Create the CI/CD workflow `.github/workflows/publish.yml` required by Flow Launcher Store policy:
1. Triggers on push of tags matching `v*.*.*` and `workflow_dispatch`.
2. Checks out repository and sets up .NET 9 SDK on `windows-latest`.
3. Executes automated tests (`dotnet test`).
4. Runs `dotnet publish Flow.Launcher.Plugin.SnapSync.csproj -c Release -r win-x64 --no-self-contained`.
5. Excludes host-provided assemblies (`Microsoft.Windows.SDK.NET.dll` and `Flow.Launcher.Plugin.dll`) per ADR 0005 to keep package size under 1MB.
6. Packages output into `Flow.Launcher.Plugin.SnapSync.zip`.
7. Publishes GitHub Release using `softprops/action-gh-release@v2` attaching the zip artifact.

## Acceptance Criteria

1. `.github/workflows/publish.yml` is created and valid YAML.
2. The workflow includes permissions `contents: write`.
3. The packaging step strips host assemblies before zipping.
4. Local dry-run packaging confirms the resulting zip contains `plugin.json`, `Flow.Launcher.Plugin.SnapSync.dll`, `SnapSync.Core.dll`, and `Images/app.png`, with compressed size under 1MB.

## Testing Seam

- YAML structure validation.
- Dry-run local PowerShell publish & archive script verifying zip structure and size.

## Demo Path

Execute dry-run packaging and inspect the generated archive structure and byte count.

## Blockers / Dependencies

Blocked by Issue 01.
