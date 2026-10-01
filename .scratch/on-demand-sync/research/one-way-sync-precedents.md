# One-Way Sync Precedents and Comparative Analysis

This report documents synchronization semantics, comparison heuristics, preview conventions, filesystem link handling, safe transfer mechanics, and overlapping directory protections across established synchronization tools: **FreeFileSync**, **Microsoft Robocopy**, and **rsync**, with platform UI integration context from **Flow Launcher**.

Every substantive technical claim is cited directly from primary sources (official documentation, product manuals, source code, or command help output). A concluding section translates these precedents into architectural implications and recommendations for SnapSync.

---

## 1. One-Way Update Semantics and Extra Destination Files

Tools differ sharply on whether "one-way update" implies pruning extraneous files on the destination, skipping newer destination files, or creating bidirectional synchronization databases.

### FreeFileSync
*Source: FreeFileSync User Manual — Synchronization Settings (`https://freefilesync.org/manual.php?topic=synchronization-settings`)*

FreeFileSync distinguishes two distinct one-way synchronization variants:
1. **Mirror**:
   - The left folder is the source and the right folder is the target.
   - Synchronizes changes such that target becomes an exact copy of the source.
   - Creates and overwrites files on target, and explicitly **deletes extraneous files and folders on target** that do not exist on source.
2. **Update**:
   - Copies new and updated files from source to backup target.
   - Strictly **never deletes extraneous files** on the target. Files deleted on the source side are not deleted on the target drive.
   - If files are deleted on the target, FreeFileSync does not copy them again if change detection with database is enabled ("files deleted on the backup drive will not be copied again").
3. **Custom**:
   - Allows fine-grained rule configuration per category: left-only, right-only, left-newer, right-newer, conflict. Actions include `Copy to right (->)`, `Copy to left (<-)`, `Delete`, or `Do nothing`.

### Microsoft Robocopy
*Source: Microsoft Learn — Robocopy Reference (`https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/robocopy`)*

Robocopy provides standard incremental copy semantics by default, with opt-in flags for deletion or protection:
- **Default one-way behavior**: Robocopy copies subdirectories (`/s` or `/e`) and skips identical files. By default, **extra files on the destination are retained untouched**.
- **`/PURGE`**: Deletes destination files and directories that no longer exist in the source.
- **`/MIR`**: Mirror mode; documented as equivalent to combining `/E` (include empty subdirectories) plus `/PURGE`. Overwrites destination directory security settings when used with `/E`.
- **`/XX` (Exclude Extra)**: Explicitly excludes extra files and directories present in destination from any processing. Extra files are neither logged as transfers nor deleted.
- **`/XN` (Exclude Newer)**: Prevents destination files that are newer than source files from being overwritten.
- **`/XO` (Exclude Older)**: Prevents destination files that are older than source files from being overwritten.
- **`/XC` (Exclude Changed)**: Excludes files with matching timestamps but differing file sizes.
- **`/XL` (Exclude Lonely)**: Excludes source files that do not exist on the destination (prevents adding new files).

### rsync
*Source: rsync(1) manpage (`https://download.samba.org/pub/rsync/rsync.1`)*

rsync defaults to additive copy and requires explicit flags for extraneous file removal:
- **Default behavior**: Copies files from source to destination without touching files in destination that are absent from source.
- **`--delete`**: Deletes extraneous files from the receiving side (those absent from sender), scoped only to directories synchronized. The manual explicitly cautions: *"This option can be dangerous if used incorrectly! It is a very good idea to first try a run using the `--dry-run` (`-n`) option to see what files are going to be deleted."* Furthermore, if the sending side detects any I/O errors during tree traversal, deletion of destination files is automatically aborted.
- **De-synchronization phases for deletion**: rsync supports `--delete-before` (delete before transferring new files), `--delete-during` (delete incrementally during recursion), `--delete-delay` (compute during transfer, delete before final rename), and `--delete-after` (delete after all transfers complete).
- **`--update` (`-u`)**: Forces rsync to skip destination files that have a modified timestamp newer than the source file. If timestamps are identical but sizes differ, the file is updated.

---

## 2. Comparison Heuristics and Timestamp Preservation

Efficient synchronization requires determining whether two files are identical without reading full file content on every sync run.

### Comparison Heuristics

| Tool | Default Quick Check | Fallback / Alternative Modes | Timestamp Granularity / Tolerance |
| :--- | :--- | :--- | :--- |
| **FreeFileSync** | Modification time + file size (`Compare by File Time and Size`). | - `Compare by File Content` (byte-for-byte; slow).<br>- `Compare by File size` (size only; for unreliable clock devices like MTP/legacy FTP). | **2-second tolerance** by default (`FileTimeTolerance Seconds="2"` in `GlobalSettings.xml`), accommodating FAT/FAT32 2-second resolution. |
| **Microsoft Robocopy** | Modification timestamp + file size. | - `/XC` (exclude changed size).<br>- `/IS` (include same files).<br>- `/IT` (include tweaked files: same time & size, differing attributes). | Default Windows NTFS timestamp comparison; `/FFT` switch forces **2-second FAT file time granularity**; `/DST` compensates for 1-hour daylight saving shift. |
| **rsync** | Modification timestamp + file size ("quick check" algorithm). | - `--checksum` (`-c`): compares 128-bit checksums instead of mod-time/size.<br>- `--size-only`: skips files matching size regardless of timestamp.<br>- `--ignore-times` (`-I`): disables quick check, updating all files. | `--modify-window=NUM` (`-@`): default `0` (integer seconds); setting `1` accommodates 2-second FAT resolution; negative value (e.g. `-1`) compares nanoseconds. |

*Sources:*
- *FreeFileSync Comparison Settings (`https://freefilesync.org/manual.php?topic=comparison-settings`)*
- *FreeFileSync Expert Settings (`https://freefilesync.org/manual.php?topic=expert-settings`)*
- *Microsoft Learn Robocopy (`https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/robocopy`)*
- *rsync(1) manpage (`https://download.samba.org/pub/rsync/rsync.1`)*

### Timestamp Preservation

- **rsync**: Modification time preservation is **not default** unless `-t` (`--times`) or `-a` (`--archive`) is specified. The manual warns: *"if this option is not used, the optimization that excludes files that have not been modified cannot be effective; in other words, a missing -t (or -a) will cause the next transfer to behave as if it used --ignore-times (-I), causing all files to be updated."*
- **Robocopy**: Default copy flags are `/COPY:DAT` (Data, Attributes, Timestamps). Directory timestamps require `/DCOPY:T` (default is `/DCOPY:DA`). The `/TIMFIX` flag corrects timestamps on files without recopying data.
- **FreeFileSync**: Automatically preserves source file modification timestamps on destination files during copy.

---

## 3. Dry-Run and Preview Conventions

Sync tools provide visibility into planned file operations before executing destructive changes.

### Robocopy (`/L`)
*Source: Robocopy manual & CLI reference (`robocopy /?`)*
- The `/L` switch ("List only") runs the full directory comparison and displays what files would be copied, deleted, or skipped without modifying disk state or updating timestamps.
- Reports classify items as `*Extra File`, `New File`, `Newer`, `Older`, `Changed`, or `Same`.
- Returns exit codes consistent with findings (e.g. `1` for files to copy, `2` for extra files found).

### rsync (`--dry-run` / `-n`)
*Source: rsync(1) manpage (`https://download.samba.org/pub/rsync/rsync.1`)*
- `--dry-run` (`-n`) performs a trial run without making any changes to destination or source.
- Produces identical logging output as an active run when combined with `--itemize-changes` (`-i`) or `--verbose` (`-v`).
- The manual states: *"The output of `--itemize-changes` is supposed to be exactly the same on a dry run and a subsequent real run (barring external changes to the source or destination and system call failures); if it isn't, that's a bug."*

### FreeFileSync Two-Stage Architecture
*Source: FreeFileSync User Manual — Quick Start (`https://freefilesync.org/manual.php?topic=freefilesync`)*
- FreeFileSync is structured by design as a two-phase workflow: **Compare** followed by **Synchronize**.
- The "Compare" stage acts as a full visual dry run: it analyzes both folders, grids every file action (left-only, right-only, conflict, overwrite, delete), displays directional arrows, and computes total transfer size/item counts before the user presses "Synchronize".

### Flow Launcher Integration Context
*Source: Flow Launcher C# Plugin Reference & API (`https://www.flowlauncher.com/docs/API-Reference/Flow.Launcher.Plugin.md`)*
- Flow Launcher plugins execute user queries via `Query(Query)` or `QueryAsync(Query, CancellationToken)`.
- Results return `Result` objects containing `Title`, `SubTitle`, `IcoPath`, and an execution callback (`Action` / `AsyncAction`).
- For on-demand sync plugins in Flow Launcher, the query view naturally functions as a **preview / dry-run**: the query calculates differences (e.g. "3 files to update, 1 file to add, 0 to delete; 4.2 MB total"), presents them in the results list, and executes the actual sync only when the user selects or presses Enter on the action item.

---

## 4. Symlink and NTFS Junction Handling

Symbolic links and directory junctions can create recursive loops, target traversal escapes, or file corruption if mishandled.

| Tool | Default Behavior | Options / Flags | Link vs. Target Semantic |
| :--- | :--- | :--- | :--- |
| **FreeFileSync** | Skips symlinks unless configured. | Choice between: (1) **Follow**: Link target is traversed and target file copied. (2) **As link**: Link object itself is copied as a symlink without traversing target. | Treats NTFS volume mount points, NTFS junction points, WSL symlinks, and Windows symlinks uniformly under symbolic link handling. Requires admin rights on Windows to create symlinks. |
| **Microsoft Robocopy** | **Follows junction points and symlinks by default**. | - `/XJ`: Excludes junction points and symlinks (both files and directories).<br>- `/XJD`: Excludes junction points/symlinks for directories.<br>- `/XJF`: Excludes symlinks for files.<br>- `/SJ`: Copies junctions as junctions instead of targets.<br>- `/SL`: Copies symlinks as links instead of targets. | Without `/XJ`, recursive copies (`/S` or `/E`) traversing directory junctions pointing upward can enter infinite loops (cyclic recursion). |
| **rsync** | Ignores symlinks with a "skipping non-regular file" warning. | - `-l` (`--links`): Recreates symlink on destination.<br>- `-L` (`--copy-links`): Collapses symlink and copies referent file/dir.<br>- `--safe-links`: Ignores symlinks pointing outside the sync tree.<br>- `--copy-unsafe-links`: Copies safe symlinks as links, collapses unsafe links into files. | Distinguishes "safe" vs. "unsafe" links (absolute links or links climbing above sync root with `..` are deemed unsafe to prevent directory traversal attacks). |

*Sources:*
- *FreeFileSync Comparison Settings — Symbolic Link Handling (`https://freefilesync.org/manual.php?topic=comparison-settings`)*
- *Microsoft Learn Robocopy Reference (`https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/robocopy`)*
- *rsync(1) manpage — Symbolic Links (`https://download.samba.org/pub/rsync/rsync.1`)*

---

## 5. Safe Copy and Partial-File Handling

When a transfer is interrupted (network disconnect, process termination, disk full), incomplete files can corrupt existing destination data.

### FreeFileSync Fail-Safe File Copy
*Sources:*
- *FreeFileSync FAQ (`https://freefilesync.org/faq.php`)*
- *FreeFileSync Release Archive & Forum (`https://freefilesync.org/archive.php`, `https://freefilesync.org/forum/viewtopic.php?t=1592`)*
- Enabled by default ("Fail-safe file copy prevents data corruption").
- FreeFileSync writes incoming data to a temporary file in the destination folder using the naming pattern `<Filename>.<random>.ffs_tmp`.
- Once the write and timestamp preservation finish successfully, FreeFileSync atomically replaces the target file via native file system renaming (`MoveFileEx` on Windows).
- If the sync is interrupted, the original destination file remains completely intact, leaving behind temporary files that are cleaned up on future runs.

### Microsoft Robocopy Restartable Mode
*Source: Microsoft Learn — Robocopy Reference (`https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/robocopy`)*
- **Default mode**: Direct copy stream into destination file. If aborted, destination contains an incomplete, truncated file.
- **`/Z` (Restartable mode)**: Uses checkpoint records during file transfer. If interrupted, subsequent Robocopy execution resumes from the last recorded block rather than starting from byte 0.
- **`/B` (Backup mode)**: Overrides ACL restrictions using `SeBackupPrivilege` and `SeRestorePrivilege`.
- **`/ZB`**: Attempts restartable mode; if access is denied, automatically falls back to backup mode.

### rsync Atomic Staging and Resume Modes
*Source: rsync(1) manpage (`https://download.samba.org/pub/rsync/rsync.1`)*
- **Default mode (Safe temporary file replacement)**:
  - By default, rsync writes the incoming file data into a temporary file in the same directory as the target file (prefixed with `.` and suffixed with a random token).
  - When the transfer completes and checksum/attributes are verified, it renames the temporary file over the destination file.
  - If interrupted mid-transfer, rsync deletes the partial temporary file by default so no corrupted partial file replaces the destination.
- **`--inplace` (Direct in-place writing)**:
  - Overwrites destination file directly without a temporary file.
  - Documented hazards: Incomplete transfers leave destination files in corrupted/truncated states; in-use binaries crash or cannot be modified; hard-linked destinations are simultaneously overwritten.
- **`--partial` / `--partial-dir=DIR`**:
  - Overrides default deletion of interrupted temporary files, keeping partial files to accelerate resumption.
  - Using `--partial-dir` stages incomplete transfers in a dedicated subdirectory (e.g. `.rsync-partial`), ensuring partial transfers do not pollute the primary folder.
- **`--delay-updates`**:
  - Holds temporary files in a `.~tmp~` directory across the entire sync session, only executing atomic renames into place in rapid succession at the very end of the sync run.

---

## 6. Overlapping Source and Destination Protections

When source and destination paths intersect (e.g., syncing a directory into itself, or into a child subdirectory), recursive file copying can create unbounded nesting loops or destructive self-deletion.

### Tool Behaviors

1. **FreeFileSync**:
   *Source: FreeFileSync Forum (`https://freefilesync.org/forum/viewtopic.php?t=4318`)*
   - FreeFileSync validates folder pairs and warns users if source and destination overlap or if folders are synchronized under multiple base pairs: *"Warning: Some files will be synchronized as part of multiple base folders. To avoid conflicts, set up exclude filters."*
   - Official documentation advises keeping base folder pairs strictly independent.
2. **Microsoft Robocopy**:
   *Source: Microsoft Learn & Robocopy CLI testing*
   - When source and destination are identical paths (`robocopy C:\dir C:\dir /E`), Robocopy executes, skips every file as identical (`Total: 1, Skipped: 1`), and exits cleanly with return code `0`.
   - When destination is an immediate or nested subdirectory of source (`robocopy C:\dir C:\dir\sub /E`), Robocopy **does not automatically abort or prevent self-nesting**. Unless explicitly blocked via `/XD C:\dir\sub`, Robocopy attempts to copy `C:\dir` into `C:\dir\sub`, encountering the destination folder as a new subdirectory in recursion, causing infinite path nesting until the path limit or disk space is exhausted.
   - Docs are silent on built-in collision prevention between source and target, leaving exclusion to operator flags (`/XD`).
3. **rsync**:
   *Source: rsync(1) manpage (`https://download.samba.org/pub/rsync/rsync.1`)*
   - rsync docs explicitly address path containment and directory traversal risks under symlinks and security boundaries (`--safe-links`, `--confine-root`).
   - If destination is a subdirectory inside source during recursive copy (`rsync -a /src/ /src/dest/`), rsync treats `dest/` as an ordinary subdirectory of `src/` and copies contents recursively into `dest/dest/...` unless excluded with `--exclude='/dest'`.
   - Official manual and guides recommend strict path exclusion or distinct root directories.

---

## 7. Implications for SnapSync

This section distinguishes primary-source facts from design recommendations for SnapSync.

### Sourced Facts Summary
1. All three tools (FreeFileSync, Robocopy, rsync) distinguish between **additive sync** (add/update files without touching extra destination files) and **mirror sync** (prune extra files to match source).
2. All three tools employ a **modification timestamp + size** quick check as the default file-comparison heuristic.
3. Both FreeFileSync and rsync employ a **2-second timestamp tolerance window** by default (or via switch) to prevent false-positive change detection on FAT/FAT32/exFAT drives.
4. Default timestamp preservation is universal for incremental sync correctness: without preserving modification timestamps on the destination, subsequent runs cannot determine whether a file was modified.
5. In Robocopy and rsync, copying directory trees where the destination is a subdirectory of the source causes infinite recursive nesting unless explicitly excluded.
6. Safe file copying via **temporary file staging followed by atomic rename** (`.ffs_tmp` in FreeFileSync, `.<filename>.XXXXXX` in rsync) is the established standard for preventing file corruption during interrupted syncs.

### Recommendations for SnapSync
*(Design interpretations derived from the facts above for SnapSync's .NET / Flow Launcher implementation)*

1. **One-Way Semantics (Additive Update vs. Mirror)**:
   - For an on-demand plugin invoked from a quick launcher, **Additive Update (non-deleting)** should be the safe default. Extraneous destination files should be retained unless the user explicitly configures a "Mirror / Purge" policy.
   - Accidental invocation of a mirror sync in a launcher interface could irreversibly wipe files on an external backup drive.
2. **Comparison Heuristics**:
   - Compare `FileInfo.Length` (size in bytes) and `FileInfo.LastWriteTimeUtc` (timestamp).
   - Adopt a **2-second tolerance window** (`Math.Abs((sourceTime - destTime).TotalSeconds) > 2.0`) to avoid perpetual resynchronization on FAT32/exFAT USB drives.
   - Always copy and preserve `LastWriteTimeUtc` from source to destination upon successful file copy (`File.SetLastWriteTimeUtc`).
3. **Dry-Run & Flow Launcher Result Representation**:
   - Leverage Flow Launcher's `QueryAsync` pipeline as the dry-run inspection phase.
   - When a sync pair is queried, scan paths and return a summary result (e.g. `Title: "Sync <PairName>"`, `SubTitle: "3 to copy, 0 to delete (14.2 MB) | Enter to execute"`).
   - Perform the actual file transfer only inside the `Result.AsyncAction` delegate when the user confirms execution.
4. **Symlink and Junction Safety**:
   - Follow rsync and Robocopy best practices by **excluding directory junctions and symlinks by default**, or skipping reparse points during `DirectoryInfo.EnumerateFileSystemInfos` traversal.
   - Traversing NTFS junctions on Windows (such as junction loops or mount points) risks infinite recursion or copying external drives into local targets.
5. **Fail-Safe File Copy**:
   - Emulate FreeFileSync's fail-safe copy: write byte streams to `<DestinationPath>.<Guid>.tmp`.
   - Upon stream flush and verification, call `File.Move(tempFile, destinationFile, overwrite: true)` for atomic replacement, and set `LastWriteTimeUtc`.
   - If an exception occurs during copy, delete the `.tmp` file in a `finally` block to avoid leaving corrupted artifacts.
6. **Overlapping Path Protection**:
   - Implement upfront path validation before comparison begins.
   - Resolve absolute, canonical full paths (`Path.GetFullPath`).
   - If `sourcePath.Equals(destinationPath, StringComparison.OrdinalIgnoreCase)` or if `destinationPath.StartsWith(sourcePath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)`, abort immediately with a clear validation error. This prevents both self-overwriting and infinite cyclic subfolder nesting.
