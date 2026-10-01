# Release And Plugin Store Reference (C#)

Official sources used:

- Testing/deploy docs: https://www.flowlauncher.com/docs/testing.md
- Official template release script: https://github.com/Flow-Launcher/dotnet-template/blob/master/Flow.Launcher.Plugin.Template/release.ps1
- Plugin manifest repository: https://github.com/Flow-Launcher/Flow.Launcher.PluginsManifest
- Node.js release docs (workflow shape, adapted for .NET): https://www.flowlauncher.com/docs/nodejs-release-project.md

## Build And Package

The official template's release path (`release.ps1`):

```powershell
dotnet publish Flow.Launcher.Plugin.YourPlugin -c Release -r win-x64 --no-self-contained
Compress-Archive -LiteralPath Flow.Launcher.Plugin.YourPlugin/bin/Release/win-x64/publish `
  -DestinationPath Flow.Launcher.Plugin.YourPlugin/bin/YourPlugin.zip -Force
```

`--no-self-contained` matters: Flow already ships the .NET runtime the plugin targets, so a framework-dependent publish keeps the zip small and version-aligned with the app.

The zip must include:

- `plugin.json`
- The plugin DLL named exactly as `ExecuteFileName`
- All dependency assemblies from the publish output (NuGet dependencies, `CopyLocalLockFileAssemblies` output)
- `Images/...` icons (every `IcoPath` referenced in code/results)
- `Languages/*.xaml` if the plugin implements `IPluginI18n`
- License/readme if the repo includes them

The zip should not include:

- `.git` and local caches (`bin/obj` intermediates other than the publish folder, `.vs`)
- Source-only clutter (`.github`, test projects, screenshots)
- Secrets or local settings files

## Version And Release Workflow

`Version` in `plugin.json` is the release source of truth. GitHub Actions workflow for a C# plugin:

```yaml
name: Publish Release

on:
  workflow_dispatch:
  push:
    branches: [main]
    paths-ignore:
      - .github/workflows/*

jobs:
  publish:
    runs-on: windows-latest

    steps:
      - uses: actions/checkout@v4

      - name: Set up .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 9.0.x   # match the TargetFramework you verified against the NuGet package

      - name: Publish
        run: dotnet publish Flow.Launcher.Plugin.YourPlugin -c Release -r win-x64 --no-self-contained

      - name: Read plugin version
        id: version
        shell: pwsh
        run: |
          $v = (Get-Content Flow.Launcher.Plugin.YourPlugin/plugin.json | ConvertFrom-Json).Version
          "version=$v" >> $env:GITHUB_OUTPUT

      - name: Create zip
        shell: pwsh
        run: |
          Compress-Archive -LiteralPath Flow.Launcher.Plugin.YourPlugin/bin/Release/win-x64/publish `
            -DestinationPath Flow.Launcher.Plugin.YourPlugin/bin/YourPlugin.zip -Force

      - name: Publish release
        uses: softprops/action-gh-release@v2
        with:
          files: Flow.Launcher.Plugin.YourPlugin/bin/YourPlugin.zip
          tag_name: v${{ steps.version.outputs.version }}
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
```

Notes:

- Use `windows-latest` — the plugin targets `-windows` TFM and RID `win-x64`.
- Adapt `dotnet-version` to the .NET major version your `TargetFramework` requires; confirm against the NuGet package's supported frameworks, not the stale official template pins.
- If the repo has tests, run them (`dotnet test`) before publishing.
- Keep the zip name aligned with the plugin/repo name.

## Plugin Store Submission

Submission is language-independent: open a PR against `Flow-Launcher/Flow.Launcher.PluginsManifest` adding `plugins/<plugin-name>-<plugin-id>.json`:

```json
{
  "ID": "your-stable-plugin-uuid",
  "Name": "Your Plugin",
  "Description": "Short description shown in the store",
  "Author": "YourName",
  "Version": "1.0.0",
  "Language": "csharp",
  "Website": "https://github.com/owner/Flow.Launcher.Plugin.YourPlugin",
  "UrlDownload": "https://github.com/owner/Flow.Launcher.Plugin.YourPlugin/releases/download/v1.0.0/YourPlugin.zip",
  "UrlSourceCode": "https://github.com/owner/Flow.Launcher.Plugin.YourPlugin/tree/main",
  "IcoPath": "https://cdn.jsdelivr.net/gh/owner/Flow.Launcher.Plugin.YourPlugin@main/Images/app.png"
}
```

Copy `ID`, `Name`, `Description`, `Author`, `Version`, `Language`, `Website` from the plugin's `plugin.json`. Add:

- `UrlDownload`: the GitHub Release zip URL (tag matches `plugin.json` `Version`)
- `UrlSourceCode`: repository URL
- `IcoPath`: globally accessible CDN URL (jsDelivr from the GitHub-hosted icon is common)
- `MinimumAppVersion`: optional, only when the plugin genuinely requires a newer Flow

**Placeholders rule**: when repository URLs/releases do not exist yet, emit clearly-marked placeholder values and list exactly which fields need real values. Never fabricate plausible-looking URLs.

After PR approval, CDN synchronization may take several days. During that window users can install manually: `pm install <zip url or local path>`.

## Store Policy

Do not create or submit plugins that facilitate malicious code, piracy, deception, inappropriate content, illegal activity, impersonation, abuse, fraud, or spam. If a user request risks one of those categories, redirect to a benign plugin design.
