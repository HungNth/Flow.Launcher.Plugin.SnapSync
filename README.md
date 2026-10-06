<div align="center">

<img src="Images/app.png" alt="SnapSync Logo" width="100" />

# SnapSync for Flow Launcher

**On-demand, one-way file and folder synchronization plugin with safety checks and dry-run preview.**

[![Platform](<https://img.shields.io/badge/platform-Windows%20(Flow%20Launcher)-blue.svg>)](#requirements)
[![Target](https://img.shields.io/badge/.NET-9.0--windows-512bd4.svg)](#requirements)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

</div>

SnapSync allows you to trigger reliable one-way file and directory synchronization directly from Flow Launcher. It pushes files from authoritative sources to exact destination paths while validating configurations against recursive loops, nested targets, and dangerous path conflicts before touching the filesystem.

---

## Features

- **On-Demand & One-Way**: Synchronizes only when explicitly triggered; never mutates sources or implicitly deletes unrelated destination files.
- **Dry-Run Preview**: Inspect projected changes and potential issues (`Ctrl+Enter`) before writing any bytes to disk.
- **Interactive Synchronization Reports**: Browse and filter operation outcomes directly inside Flow Launcher (`sy :report`), with one-click path copying to the clipboard.
- **Fail-Safe Replacement**: Stages copies through temporary files with atomic replacement to prevent partial or corrupted writes.
- **Structural Path Safety**: Preflight validation catches recursive paths, overlapping roots, and nested destinations before execution starts.
- **Navigation Link Safety**: Avoids traversing navigation links (symlinks, junctions) to prevent filesystem loops, while treating non-navigation reparse points (such as cloud placeholders) as regular file entries.
- **Multiple Destinations & Flexible Comparison**: Push a single source to multiple targets using either fast timestamp/size tolerance or cryptographic SHA-256 hashing.
- **Single-Flight Concurrency**: Prevents overlapping runs with responsive cancellation support.

---

## Requirements

- [Flow Launcher](https://www.flowlauncher.com/) v1.19.0 or later
- Windows 10 / 11 (x64)
- [.NET 9 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0) (Windows)

---

## Installation

### For Users

1. Download the latest `Flow.Launcher.Plugin.SnapSync.zip` from [GitHub Releases](https://github.com/HungNth/Flow.Launcher.Plugin.SnapSync/releases).
2. Extract the archive into your Flow Launcher plugins directory:
    ```text
    %APPDATA%\FlowLauncher\Plugins\Flow.Launcher.Plugin.SnapSync
    ```
3. Restart Flow Launcher or run `Reload Plugins`.

### For Developers

To build from source:

```powershell
git clone https://github.com/HungNth/Flow.Launcher.Plugin.SnapSync.git
cd Flow.Launcher.Plugin.SnapSync
dotnet publish Flow.Launcher.Plugin.SnapSync.csproj -c Release -r win-x64 --no-self-contained
```

Alternatively, you can use the development deployment script:

```powershell
.\debug.ps1
```

> [!WARNING]
> Running `debug.ps1` will forcefully stop any running `Flow.Launcher.exe` process, replace the installed plugin files under `%APPDATA%\FlowLauncher\Plugins\Flow.Launcher.Plugin.SnapSync`, and restart Flow Launcher immediately. Ensure your work in Flow Launcher is saved before running this script.

---

## Usage

SnapSync uses the action keyword **`sy`**.

### Command Reference

| Query / Key                    | Description                                                                                           |
| :----------------------------- | :---------------------------------------------------------------------------------------------------- |
| `sy`                           | List all configured Sync Profiles.                                                                    |
| `sy <profile-name>`            | Filter profiles by name.                                                                              |
| `Enter` (on profile)           | Synchronize the selected profile. _(If the profile is disabled, opens Settings instead.)_             |
| `Ctrl+Enter` (on profile)      | Run a dry-run **Preview** showing projected changes.                                                  |
| `Shift+Enter` (or Right-Click) | Open the context menu (**Sync now**, **Preview changes**, or **Open Settings**).                      |
| `sy all`                       | Synchronize all **enabled** profiles sequentially. _(Does not support dry-run preview)._              |
| `sy :report [filter]`          | View entries from the latest **Synchronization Report**. Filter by filename, destination, or outcome. |

### Inspecting Synchronization Reports

After running a Preview or Synchronization, SnapSync retains the latest report in memory:

1. Type `sy :report` to inspect copied, unchanged, and failed entries.
2. Filter the report by status or keyword, e.g.:
    - `sy :report error`
    - `sy :report Copied`
    - `sy :report .json`
3. Press `Enter` on any entry to copy its file path to your clipboard.

### Cancelling Operations

If a synchronization or preview is in progress, querying `sy` displays a busy notification. Select **Cancel current operation** to request cancellation gracefully. SnapSync will stop further file processing and return a partial report for completed items.

---

## Configuration & Profile Setup

Open settings by selecting the gear icon in Flow Launcher, pressing `Enter` on an unconfigured prompt, or running `sy` and choosing **Open Settings** via the context menu.

### Key Concepts

- **Sync Profile**: A named, reusable collection of Sync Items (e.g., `Documents Backup`, `Project Assets`).
- **Sync Item**: A synchronization rule linking one **Source** to one or more **Destinations**.
    - **Source**: The authoritative file or directory path.
    - **Destinations**: One or more exact target paths. SnapSync copies items directly into the specified destination root without appending arbitrary source folder names.
    - **Item Type**: `Auto` (inferred from path), `File`, or `Directory`.
    - **Comparison Mode**:
        - `Fast (Length and timestamp tolerance)` _(Default)_: Compares file size and modification timestamps with a 2-second tolerance.
        - `SHA-256 (Cryptographic content hash)`: Streams file contents and compares cryptographic SHA-256 hashes.
    - **Exclusions**: Glob patterns (one per line, e.g., `*.tmp`, `node_modules`, `bin/**`, `.git`) to exclude matching subtrees.

> [!IMPORTANT]
> **Exact Destination Paths**: SnapSync treats destinations as exact targets. If your source directory is `D:\Notes` and you want files placed under `E:\Backups\Notes`, specify `E:\Backups\Notes` explicitly as the destination path.

---

## Safety & Invariants

SnapSync is designed around strict non-destructive safety principles:

- **Non-Destructive Synchronization**: SnapSync copies new or changed source files to destinations. It never deletes unrelated existing destination files or source files.
- **Fail-Safe Replacement**: Files are copied to temporary files before atomic replacement, protecting against incomplete writes or corruption during interrupted transfers.
- **Preflight Path Conflict Checks**: Before writing any file, SnapSync checks for overlapping sources and destinations, circular chains, or nested destination paths, halting immediately if unsafe conditions are detected.
- **Navigation Links vs. Reparse Points**: SnapSync skips navigation links (symlinks, junctions, directory mount points) to prevent recursive filesystem loops. Non-navigation reparse entries (such as cloud placeholders) are treated as standard files; if an offline placeholder cannot be read or hydrated by the OS/provider during transfer, the error is recorded per-entry in the report without halting the remaining sync.
