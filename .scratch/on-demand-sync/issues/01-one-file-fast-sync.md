# 01: One-file Fast synchronization end to end

**What to build:** A user can configure one enabled file Sync Item, find its Sync Profile with `sy`, press Enter, and safely push a new or changed Source file to one exact Destination. This slice establishes the Flow-independent core boundary, Flow adapter, versioned settings storage, xUnit seam, Fast comparison, fail-safe replacement, and concise completion notification.

**Blocked by:** None (can start immediately).

**Parent specification:** [SnapSync On-Demand Synchronization](../spec.md)

**Status:** resolved

- [x] The solution has a Flow-independent core library, a Flow Launcher/WPF adapter, and a separate xUnit test project.
- [x] The settings panel can create and save one enabled Sync Profile containing one enabled File Sync Item with one exact Destination.
- [x] Configuration persists through Flow JSON storage with schema version 1 and reloads after Flow restarts.
- [x] `sy` lists the configured profile and fuzzy-searches only loaded configuration; Query performs no Source enumeration, hashing, directory creation, or copying.
- [x] Enter synchronizes the selected profile only after explicit selection.
- [x] A missing Destination file is created; a changed Destination file is replaced; an unchanged file is skipped.
- [x] Fast comparison uses length and `LastWriteTimeUtc` with a two-second tolerance and preserves Source `LastWriteTimeUtc` after copy.
- [x] Copying streams through a temporary file beside the Destination and leaves an existing Destination intact when staging fails.
- [x] Extra files near the Destination are never deleted, and no synchronization-related watcher, poller, timer, or background service is introduced.
- [x] The user receives a concise copied/unchanged/failed summary.

## Testing seam

Exercise the public core validate and synchronize operations against real temporary files. Assert resulting bytes, timestamps, unchanged behavior, old-Destination preservation after a staged failure, and structured counters. Do not test forwarding or source text.

## Demo path

Create a profile for one temporary configuration file, run `sy <profile>` in Flow, observe the first copy, edit the Source, observe replacement, then run again and observe an unchanged summary.

## Verification evidence

- Core suite: 12 passed, 0 failed, 0 skipped. The permanent locked-Destination regression uses FileShare.None and verifies a Failed result, original bytes, and staging-file cleanup.
- Solution build: 0 warnings and 0 errors after excluding nested projects from plugin compilation and awaiting the Flow AsyncAction.
- Real-filesystem runtime smoke: first copy, unchanged, changed replacement, timestamp preservation, and locked-target preservation passed.
- Installed Flow initialized SnapSync (startup log: 71 ms); actual `sy` query displayed the no-profiles/settings result through Windows UI Automation.
- Initial automation could not verify settings interaction: SendKeys and child-button invocation did not open settings; SetForegroundWindow returned false. A direct probe of the visible SnapSync ListBoxItem found SelectionItem, ScrollItem, and SynchronizedInput patterns only; requesting InvokePattern raised Unsupported Pattern. Exactly one Flow process (PID 34712) was observed before this probe. No additional deployment or restart is authorized.
- User subsequently confirmed that pressing Enter on "SnapSync: No profiles configured" opens settings, superseding the settings-opening limitation of the automation probe.
- User-confirmed `sy Smoke` Enter run displayed "SnapSync: Smoke Completed" with Copied 1, Unchanged 0, Failed 0. The resulting target.txt contains the expected Source bytes. Flow JSON storage at Settings/Plugins/SnapSync.Core/SnapSyncConfiguration.json contains SchemaVersion 1 and enabled Smoke/File objects with the exact configured Source and Destination. Abbreviated fuzzy search and reload after restart remain unverified.
- User confirmed the repeat run used abbreviated query `sy smk`, found Smoke, and reported Copied 0, Unchanged 1, Failed 0. This verifies abbreviated fuzzy search and the same-session Unchanged notification. Reload persistence after a separately authorized restart remains unverified.
- The passing permanent extra-file test confirms unrelated Destination files are preserved. A code-level search of Main, ViewModels, Views, and src found no FileSystemWatcher, PeriodicTimer, Timer, Task.Run, BackgroundService, Thread, or while-loop background work.
- User authorized one Flow process restart solely for reload-persistence verification; no deployment or plugin-directory changes are authorized.
- Authorized process-only restart completed using the actual versioned executable: PID 34712 was replaced by PID 32216. SHA-256 of persisted configuration was unchanged (42305DA75DDA3FB1CA32DD72BFFC170718F9548941DE4E83842AB2D3842AAEB4). UI Automation could not see the hidden query control; the subsequent manual query below supplies reload evidence.
- User confirmed `sy smk` still finds Smoke after the authorized restart without reopening settings. PID 32216 is alive; the fresh Flow startup log records SnapSync initialization at 19:05:47 with 14 ms cost. Together with the unchanged persisted JSON, this verifies reload persistence.
- Flow logged Notification.ShowInternal UriFormatException during the two earlier sync notifications. Main now omits the custom icon argument and uses Flow's default notification icon. Notification acceptance remains open until authorized deployment/restart and a post-fix notification/log smoke check.
- User authorized deployment/restart for the notification fix. Pre-deploy core suite passed 12/12; publish succeeded. Deployment copied files without deleting the plugin directory, preserved the configuration SHA-256, and replaced PID 32216 with PID 28028. Deployed plugin DLL matches published output (SHA-256 A99B9582B51E6DECCBA7EF13B76FEBC073297290CC3AD0F07513199AC69D2BA5); fresh log records SnapSync initialization at 19:11:54 (52 ms). Post-fix notification action remains pending; startup alone is not notification proof.
- User ran `sy smk`, selected Smoke, and confirmed the repaired notification appeared without error. Flow log entries from the repaired deployment startup (19:11:53 onward) contain no new Notification.ShowInternal or UriFormatException. All ticket-01 acceptance criteria now have implementation and runtime evidence.

## Answer

Delivered the one-file Fast synchronization slice through the public core API and Flow settings/query/Enter actions. The real-filesystem suite passes 12/12; solution build and publish succeed; manual Flow acceptance verifies Save, initial copy, Unchanged, abbreviated fuzzy search, configuration reload, and the repaired notification.
