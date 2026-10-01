# Flow Launcher C# Plugin Reference

This reference condenses the official Flow Launcher docs for C#/.NET plugins. Sources used (all official):

- Development guide: https://www.flowlauncher.com/docs/develop-dotnet-plugins.md
- plugin.json manifest: https://www.flowlauncher.com/docs/plugin.json.md
- API reference index: https://www.flowlauncher.com/docs/API-Reference/Flow.Launcher.Plugin.md
- Testing guide: https://www.flowlauncher.com/docs/testing.md
- JSON schema for plugin.json: https://www.flowlauncher.com/schemas/plugin.schema.json
- Official dotnet template: https://github.com/Flow-Launcher/dotnet-template
- Official C# sample plugin: https://github.com/Flow-Launcher/plugin-samples
- NuGet package (version source of truth): https://www.nuget.org/packages/Flow.Launcher.Plugin

For any API detail not covered here, the API reference index above links a per-type page for every public type in `Flow.Launcher.Plugin`.

## Plugin Model

C# plugins are .NET assemblies loaded in-process by Flow. The plugin directory needs at minimum:

1. `plugin.json` (manifest)
2. A .NET assembly implementing `IPlugin` or `IAsyncPlugin`, referencing the `Flow.Launcher.Plugin` NuGet package.

(From the development guide, first section.)

## IPlugin / IAsyncPlugin

From https://www.flowlauncher.com/docs/API-Reference/Flow.Launcher.Plugin/IPlugin.md and the development guide:

- `IPlugin` (sync): `void Init(PluginInitContext context)` + `List<Result> Query(Query query)`.
- `IAsyncPlugin` (async): `Task InitAsync(PluginInitContext context)` + `Task<List<Result>> QueryAsync(Query query, CancellationToken token)`.
- The docs recommend `IAsyncPlugin` when query/init requires high IO or CPU-intensive work; `QueryAsync`'s `CancellationToken` fires when the user types a new query.
- `Init`/`InitAsync` runs before the first `Query` and in parallel with other plugins' init — do expensive preparation here, not in the constructor.
- Per the development guide, implement feature interfaces in the **same class** that implements `IPlugin`/`IAsyncPlugin`.

## Query

From https://www.flowlauncher.com/docs/API-Reference/Flow.Launcher.Plugin/Query.md:

| Property | Use |
|---|---|
| `Search` | The real query, excluding the action keyword. **Use this** — the docs recommend against `RawQuery`. |
| `RawQuery` | Includes the action keyword. Avoid directly. |
| `FirstSearch`, `SecondSearch`, `ThirdSearch` | Space-split segments of `Search`. |
| `SecondToEndSearch` | From the second segment to the end. |
| `SearchTerms` | Full string array of segments. |
| `ActionKeyword` | The keyword that triggered this plugin. |

## Result

From https://www.flowlauncher.com/docs/API-Reference/Flow.Launcher.Plugin/Result.md:

| Property | Notes |
|---|---|
| `Title` | Required. |
| `SubTitle` | Optional secondary text. |
| `IcoPath` | Icon path relative to the plugin directory, e.g. `Images\\app.png`. |
| `Action` | `Func<ActionContext, bool>` — executes on selection. Return `true` to hide Flow's window. |
| `AsyncAction` | `Func<ActionContext, ValueTask<bool>>` — async variant; `ExecuteAsync` prefers `AsyncAction` and falls back to `Action`. Verified in source: https://github.com/Flow-Launcher/Flow.Launcher/blob/dev/Flow.Launcher.Plugin/Result.cs (the generated API docs omit it; the source is authoritative). |
| `Score` | Result priority; higher ranks first (default 0). |
| `AutoCompleteText` | Text Flow uses when the user accepts autocomplete (defaults to `Title`). |
| `ContextData` | Arbitrary data attached to the result, e.g. for context-menu actions. |
| `TitleHighlightData` | Character indexes to highlight in the title. |
| `Glyph` | `GlyphInfo` font-icon; takes priority over `IcoPath` when the user enables glyph icons. |
| `TitleToolTip`, `SubTitleToolTip` | Tooltips on hover. |
| `ActionKeywordAssigned` | Keyword that produced this result; empty for global `*` results. |

`ActionContext.SpecialKeyState` exposes `CtrlPressed`/`ShiftPressed`/`AltPressed`/`WinPressed` — useful for modifier-sensitive actions (source: `ActionContext.cs` in the Flow.Launcher.Plugin source tree).

## PluginInitContext

From https://www.flowlauncher.com/docs/API-Reference/Flow.Launcher.Plugin/PluginInitContext.md:

- `context.API` — the `IPublicAPI` instance.
- `context.CurrentPluginMetadata` — this plugin's `PluginMetadata` (ID, name, action keywords, directories).

## IPublicAPI (highlights)

From https://www.flowlauncher.com/docs/API-Reference/Flow.Launcher.Plugin/IPublicAPI.md — full list there:

- UI: `ShowMsg(title, text)`, `ShowMsgError`, `ChangeQuery`, `ShowMainWindow`.
- Shell/web: `ShellRun(cmd)`, `OpenUrl(url)`, `OpenDirectory(path)`, `CopyToClipboard(text)`, `HttpDownloadAsync`, `HttpGetStringAsync`, `HttpGetStreamAsync`.
- Search: `FuzzySearch(query, string)` — Flow's own fuzzy matcher, useful for filtering your own lists consistently with Flow's ranking.
- Logging: `LogDebug`, `LogInfo`, `LogWarn`, `LogException(pluginName, message, exception)` — `LogException` is the primary logging method; it throws in debug builds so developers notice.
- Settings: `LoadSettingJsonStorage<T>()`, `SaveSettingJsonStorage<T>()`.
- Plugin metadata: `GetAllPlugins()`, `AddActionKeyword`, `RemoveActionKeyword`.
- Misc: `GetTranslation(key)`, `ReloadAllPluginData()`, `RestartApp()`, `SavePluginSettings()`, `RegisterGlobalKeyboardCallback`/`RemoveGlobalKeyboardCallback`.

## Feature Interfaces

All from the development guide's "Additional interfaces" section and linked API pages. Implement on the same class as `IPlugin`/`IAsyncPlugin`:

| Interface | Method | Purpose |
|---|---|---|
| `IContextMenu` | `List<Result> LoadContextMenus(Result selected)` | Right-click context menu for a result. Return results just like `Query`; their `Action` fires on click. Use `selected.ContextData` to carry per-result data. |
| `IReloadable` | `void ReloadData()` | Called when the user runs "Reload Plugin Data" (`sys` plugin). Rebuild in-memory caches. |
| `IAsyncReloadable` | `Task ReloadDataAsync()` | Async variant. |
| `IPluginI18n` | `GetTranslatedPluginTitle()`, `GetTranslatedPluginDescription()` | Plugin is internationalized; Flow loads `/Languages/*.xaml` resource dictionaries. Read strings via `API.GetTranslation(key)`. |
| `IResultUpdated` | event `ResultUpdated` + `ResultUpdatedEventArgs` | Early-return partial results for long-running queries — invoke the event with the current `Query` and a partial result list; Flow displays them while the query continues. |
| `IDisposable` | `Dispose()` | Cleanup when Flow exits (Flow 1.8.0+). |
| `ISettingProvider` | `CreateSettingPanel()` | Custom settings UI; returns a WPF `Control`. |
| `ISavable` | `Save()` | Persist additional plugin data at shutdown when you need more than JsonStorage. |

## Settings (C#-correct)

**`SettingsTemplate.yaml` does not apply to C# plugins.** Flow's source shows `SettingsTemplate.yaml` is read only by `JsonRPCPluginBase.InitSettingAsync` (https://github.com/Flow-Launcher/Flow.Launcher/blob/dev/Flow.Launcher.Core/Plugin/JsonRPCPluginBase.cs) — the base class for Python/JS/TS/executable plugins. .NET plugins are loaded as assemblies and never go through it. Built-in C# plugins (e.g. Calculator, https://github.com/Flow-Launcher/Flow.Launcher/tree/dev/Plugins/Flow.Launcher.Plugin.Calculator) use `Settings.cs` + `ISettingProvider` panels with no SettingsTemplate.yaml.

C# settings mechanisms:

1. **JsonStorage** (simple, recommended default):

```csharp
public class Settings
{
    public int MaxResults { get; set; } = 10;
    public string ApiKey { get; set; } = "";
}

// In InitAsync:
_settings = context.API.LoadSettingJsonStorage<Settings>();
// defaults live in the class; Flow stores user overrides as JSON
// and auto-saves loaded types on exit; call SaveSettingJsonStorage<Settings>()
// when you mutate _settings and need it persisted immediately.
```

2. **`ISettingProvider`** (custom panel): implement `CreateSettingPanel()` returning a WPF control, typically a `Control` subclass with a ViewModel. Follow the Calculator plugin's `Settings.cs` + `Views/`/`ViewModels/` layout.

## Internationalization

From the development guide (IPluginI18n section):

- Create `Languages/en.xaml` (and other language codes) as WPF `ResourceDictionary` files of key/value `system:String` pairs. Language codes follow Flow's `AvailableLanguages.cs`: https://github.com/Flow-Launcher/Flow.Launcher/blob/dev/Flow.Launcher.Core/Resource/AvailableLanguages.cs
- Example file from the official sample: https://github.com/Flow-Launcher/plugin-samples/blob/master/HelloWorldCSharp/Languages/en.xaml
- Read strings with `context.API.GetTranslation("key")`.
- Implement `IPluginI18n` (`GetTranslatedPluginTitle`, `GetTranslatedPluginDescription`) so Flow also localizes the plugin's name/description in its UI.

## Complete Sample

Official minimal sample (IPlugin + IPluginI18n + action + Languages): https://github.com/Flow-Launcher/plugin-samples/blob/master/HelloWorldCSharp/Main.cs

```csharp
using System.Collections.Generic;
using Flow.Launcher.Plugin;

public class Main : IPlugin
{
    internal PluginInitContext Context;

    public void Init(PluginInitContext context) => Context = context;

    public List<Result> Query(Query query)
    {
        return new List<Result>
        {
            new Result
            {
                Title = "Hello World from CSharp",
                SubTitle = $"Query: {query.Search}",
                IcoPath = "Images/app.png",
                Action = _ =>
                {
                    Context.API.ShowMsg("Greetings", "Hi from the sample");
                    return true;
                }
            }
        };
    }
}
```

## Pitfalls

1. **Stale official template pins.** The dotnet template pins `Flow.Launcher.Plugin 4.4.0` and `net7.0-windows` (https://github.com/Flow-Launcher/dotnet-template). The current package is newer and targets a newer .NET (check https://www.nuget.org/packages/Flow.Launcher.Plugin — 5.3.1 / `net9.0-windows7.0` as of 2026-06). Check the package, not the template.
2. **`RawQuery` vs `Search`.** Docs explicitly recommend `Search`; `RawQuery` includes the action keyword and breaks when the plugin is switched between exclusive/generic mode.
3. **Constructor work.** Plugin construction happens during serial plugin loading; expensive work belongs in `Init`/`InitAsync` which runs in parallel. (Development guide, `Init` remarks.)
4. **Duplicate plugin IDs.** If two installed plugins share an `ID`, neither loads (testing guide). Never reuse or regenerate an existing plugin's ID.
5. **Forgetting restart after rebuild.** .NET plugins are loaded into the Flow process; testing docs note Flow must be restarted after each rebuild for .NET plugins (Python/JS hot-reload by contrast).
6. **Blocking in `Query`.** A sync `Query` that does network I/O freezes Flow's search. Use `IAsyncPlugin` + `QueryAsync` and honor the `CancellationToken`.
7. **`AsyncAction` not in the generated docs.** It exists in source (`Result.cs`); see the Result table above.
8. **Missing `CopyLocalLockFileAssemblies`/content copies in csproj.** The publish output must contain `plugin.json`, icons, language files, and any dependency DLLs — without `CopyToOutputDirectory` entries and `CopyLocalLockFileAssemblies`, Flow gets an incomplete folder. Follow the template csproj: https://github.com/Flow-Launcher/dotnet-template/blob/master/Flow.Launcher.Plugin.Template/Flow.Launcher.Plugin.MyFlowPlugin/Flow.Launcher.Plugin.MyFlowPlugin.csproj

## Local Smoke Tests

1. `dotnet build` — must succeed with zero errors.
2. `dotnet publish -c Debug -r win-x64 --no-self-contained` — output folder must contain: your DLL, `plugin.json`, `Images/`, `Languages/` (if any), and dependency assemblies.
3. Manifest sanity: parse `plugin.json`; require `ID`, `Name`, `Version` (x.y.z), `Language: "csharp"`, `ExecuteFileName` equal to the built DLL filename, `IcoPath` file present in the output.
4. If Flow is installed: stop Flow (`Stop-Process -Name "Flow.Launcher"`), delete any same-ID plugin folder under `%APPDATA%\FlowLauncher\Plugins\`, copy the publish output in, start Flow, trigger the action keyword. Check `%APPDATA%\FlowLauncher\Logs\` for plugin load errors.
