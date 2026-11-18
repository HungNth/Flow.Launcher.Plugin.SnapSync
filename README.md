# SnapSync for Flow Launcher

SnapSync copies new or changed files from a source to one or more destinations when you run a sync. It leaves source files and unrelated destination files in place.

## Install and use

To install from the Plugin Store, type this in the Flow Launcher search box and press Enter.

```text
pm install SnapSync by HungNth
```

If SnapSync doesn't appear, [install it manually](#manual-installation).

1. Type `sy` and press `Enter` on the setup prompt to open SnapSync settings.
2. Create a profile and add a sync item with a source file or folder and at least one destination. Enter the full target path. For example, to copy `D:\Notes` into `E:\Backups\Notes`, set the destination to `E:\Backups\Notes`.
3. Type `sy <profile-name>` and select the profile. Press `Ctrl+Enter` to preview changes without writing files, then `Enter` to sync. On a disabled profile, `Enter` opens settings.

| In Flow Launcher | What it does |
| --- | --- |
| `sy` | List profiles. During a sync or preview, select **Cancel current operation** to stop and get a partial report. |
| `Shift+Enter` or right-click on a profile | Open the context menu to sync, preview, or edit the profile. |
| `sy all` | Sync enabled profiles one at a time. To preview, select a profile individually. |
| `sy :report [filter]` | View the latest sync or preview report. Filter by filename, destination, or outcome, such as `sy :report Copied` or `sy :report .json`. Press `Enter` to copy the destination path, or the source path if there is no destination. |

## Profile options

- Item type can be `Auto` (infer from the path), `File`, or `Directory`.
- `Fast` (default) compares file size and modification time with a 2-second tolerance. `SHA-256` compares hashes of the file contents.
- Add one exclusion pattern per line, for example `*.tmp`, `node_modules`, `bin/**`, or `.git`.

SnapSync checks for overlapping paths, cycles, and nested destinations before copying. It copies to a temporary file before replacing a destination file. It skips symlinks, junctions, and directory mount points. If a cloud placeholder cannot be read, SnapSync records the error and continues with the remaining files.

## Requirements

- [Flow Launcher](https://www.flowlauncher.com/) 2.0.0 or later
- Windows 10 or 11 (x64)
- [.NET 9 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0) for Windows

## Manual installation

1. Download `Flow.Launcher.Plugin.SnapSync.zip` from [GitHub Releases](https://github.com/HungNth/Flow.Launcher.Plugin.SnapSync/releases).
2. Extract it to `%APPDATA%\FlowLauncher\Plugins\Flow.Launcher.Plugin.SnapSync`.
3. Restart Flow Launcher.

## Build from source

```powershell
git clone https://github.com/HungNth/Flow.Launcher.Plugin.SnapSync.git
cd Flow.Launcher.Plugin.SnapSync
dotnet publish Flow.Launcher.Plugin.SnapSync.csproj -c Release -r win-x64 --no-self-contained
```

For local deployment, run `.\debug.ps1`.

> [!WARNING]
> Save your work in Flow Launcher before running the script. It forcibly stops Flow Launcher, replaces the installed plugin files, and restarts Flow Launcher.

[MIT License](LICENSE)
