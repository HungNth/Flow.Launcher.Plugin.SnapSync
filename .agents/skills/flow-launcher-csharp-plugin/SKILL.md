---
name: flow-launcher-csharp-plugin
description: Build, scaffold, debug, package, or release Flow Launcher plugins written in C# (.NET). Use this skill whenever the user mentions Flow Launcher plugins in C#, Flow.Launcher.Plugin NuGet package, IPlugin, IAsyncPlugin, PluginInitContext, plugin.json with Language csharp, ActionKeyword, Flow plugin store submission, or asks to create, port, fix, test, package, or publish a Flow Launcher C#/.NET plugin, even if they only say "flow plugin". Also use when the user asks which language to build a Flow plugin in and C#/.NET is a candidate.
---

# Flow Launcher C# Plugin Development

Use this skill to create and maintain Flow Launcher plugins written in C#. C# plugins are in-process .NET assemblies loaded directly by Flow — the highest-performance plugin type, with no subprocess or protocol overhead.

Official docs summarized here:
- `develop-dotnet-plugins`: C# plugins communicate with Flow directly, no extra protocols.
- `plugin.json`: every plugin needs a manifest in the plugin root; `Language: "csharp"`, `ExecuteFileName` is the built DLL.
- API reference: `IPlugin`/`IAsyncPlugin`, `Query`, `Result`, `PluginInitContext`, `IPublicAPI`, feature interfaces.
- Testing: deploy into Flow's Plugins directory; .NET plugins require a Flow restart per rebuild.

Read `references/csharp-plugin-reference.md` before scaffolding or changing a nontrivial plugin. Read `references/release-and-store.md` when the task includes packaging, GitHub Actions, GitHub Releases, or Plugin Store publication.

## Working Approach

1. Classify the task: new plugin, existing plugin change, bug fix, settings work, release automation, or store submission.
2. Inspect the repository first. Preserve existing structure, csproj settings, icon naming, and release workflow unless there is a concrete reason to change them.
3. Clarify only high-risk missing details such as credentials, destructive actions, or a plugin name/action keyword that cannot be inferred. Otherwise choose conservative defaults and implement.
4. Keep Flow's UX in mind: queries must be fast, `Init` runs in parallel with other plugins, results should feel instant, and errors should become useful Flow results or logged messages — not crashes.

## Core Contract

A C# plugin is a .NET class library referencing the `Flow.Launcher.Plugin` NuGet package, with a class implementing `IPlugin` (sync) or `IAsyncPlugin` (async):

```csharp
public class Main : IAsyncPlugin
{
    private PluginInitContext _context;

    public Task InitAsync(PluginInitContext context)
    {
        _context = context;
        return Task.CompletedTask;
    }

    public async Task<List<Result>> QueryAsync(Query query, CancellationToken token)
    {
        // query.Search is the real user query (no action keyword).
        // Check token for cancellation when the user types a new query.
        return new List<Result>
        {
            new Result
            {
                Title = "Hello",
                SubTitle = $"You typed: {query.Search}",
                IcoPath = "Images\\app.png",
                Action = _ => { _context.API.ShowMsg("Done"); return true; }
            }
        };
    }
}
```

Key rules:
- Prefer `IAsyncPlugin` for new code; `QueryAsync` receives a `CancellationToken` — pass it into async calls or check it periodically.
- Put expensive work (caching, indexing, I/O) in `Init`/`InitAsync`, not the constructor — `Init` runs in parallel with other plugins.
- `Action`/`AsyncAction` returns `true` to hide Flow's window after the result is selected, `false` to keep it open.
- Every `Result` needs a `Title`; icons use plugin-relative `IcoPath`.

## Manifest Rules

`plugin.json` lives in the plugin root:

```json
{
  "ID": "STABLE-UUID-GENERATED-ONCE",
  "ActionKeyword": "kw",
  "Name": "Plugin Name",
  "Description": "Short user-facing description",
  "Author": "Author",
  "Version": "1.0.0",
  "Language": "csharp",
  "Website": "https://github.com/owner/repo",
  "IcoPath": "Images\\app.png",
  "ExecuteFileName": "Flow.Launcher.Plugin.YourPlugin.dll"
}
```

- `ID` is a stable UUID. Generate it once; never regenerate for an existing plugin — Flow uses it for updates and duplicate IDs cause *no* plugin to load.
- `Language: "csharp"`. `ExecuteFileName` must name the built DLL.
- `ActionKeyword: "*"` only for deliberate global-search plugins — those need stronger scoring and filtering because they run on every query.

## Settings (C#-correct — read this before adding any)

`SettingsTemplate.yaml` is a JSON-RPC-plugin mechanism (Python/JS/TS/executable). Flow's source confirms .NET plugins never load it — `JsonRPCPluginBase` reads it, and .NET plugins do not derive from that base. Do not create one for a C# plugin.

C# settings use the plugin API:
- `context.API.LoadSettingJsonStorage<T>()` / `SaveSettingJsonStorage<T>()` — persisted JSON settings with automatic save on Flow exit for loaded types. `T` is your settings class (default values in C# code, not a YAML template).
- `ISettingProvider.CreateSettingPanel()` — custom WPF settings panel for anything the JSON storage cannot express. See the built-in Calculator plugin (`Settings.cs` + `Views/`/`ViewModels/`) for the pattern.

## Project Shape

```text
Flow.Launcher.Plugin.YourPlugin/
├── plugin.json
├── Flow.Launcher.Plugin.YourPlugin.csproj
├── Main.cs (or YourPlugin.cs)
├── Images/app.png
└── Languages/*.xaml        # only with IPluginI18n
```

csproj essentials (adapt from the official `Flow.Launcher.Plugin.Template`):

```xml
<TargetFramework>net9.0-windows</TargetFramework>
<AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
<Content Include="plugin.json"><CopyToOutputDirectory>Always</CopyToOutputDirectory></Content>
<PackageReference Include="Flow.Launcher.Plugin" Version="5.3.1" />
```

**Do not trust the official dotnet template's version pins.** The template pins `Flow.Launcher.Plugin 4.4.0` / `net7.0-windows`, which is stale. Check the latest package version on NuGet (https://www.nuget.org/packages/Flow.Launcher.Plugin) and match `TargetFramework` to it (5.3.1 → `net9.0-windows`). The runtime Flow loads is the one the *current* Flow app ships, so the package version is the source of truth.

Scaffold with the template if convenient — `dotnet new install Flow.Launcher.Plugin.Template` then `dotnet new flow-plugin --name YourPlugin --keyword kw --pluginAuthor YourGithubUsername` — then fix the package version and TargetFramework before building.

## Verification

Ladder — do what the environment allows:

1. **Minimum (no Flow installed)**: `dotnet build` succeeds, publish output contains the DLL + `plugin.json`, manifest sanity checks pass (required fields present, `ExecuteFileName` matches the built DLL name, `Language` is `csharp`, icon file exists at `IcoPath`).
2. **Local deploy (Flow installed)**: `dotnet publish -c Debug -r win-x64 --no-self-contained`, copy the publish output to Flow's Plugins folder (type `userdata` in Flow to locate it; remove any same-ID plugin folder first — duplicate IDs load nothing), restart Flow, exercise the action keyword.
3. .NET plugins require a Flow **restart** on every rebuild — unlike Python/JS plugins, there is no hot reload.

Logs: `%APPDATA%\FlowLauncher\Logs\` — check for load failures and plugin exceptions.

## Final Response

Summarize the plugin name, action keyword, entry class, important files changed, and verification performed. State anything not verifiable without Flow Launcher (e.g., runtime behavior). If release/store work was requested, state the artifact name, tag/version, and manifest fields that still need real URLs or CDN icons.
