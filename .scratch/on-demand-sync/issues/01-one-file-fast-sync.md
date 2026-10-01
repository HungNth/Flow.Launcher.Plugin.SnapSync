# 01: One-file Fast synchronization end to end

**What to build:** A user can configure one enabled file Sync Item, find its Sync Profile with `sy`, press Enter, and safely push a new or changed Source file to one exact Destination. This slice establishes the Flow-independent core boundary, Flow adapter, versioned settings storage, xUnit seam, Fast comparison, fail-safe replacement, and concise completion notification.

**Blocked by:** None (can start immediately).

**Parent specification:** [SnapSync On-Demand Synchronization](../spec.md)

**Status:** ready-for-agent

- [x] The solution has a Flow-independent core library, a Flow Launcher/WPF adapter, and a separate xUnit test project.
- [ ] The settings panel can create and save one enabled Sync Profile containing one enabled File Sync Item with one exact Destination.
- [ ] Configuration persists through Flow JSON storage with schema version 1 and reloads after Flow restarts.
- [ ] `sy` lists the configured profile and fuzzy-searches only loaded configuration; Query performs no Source enumeration, hashing, directory creation, or copying.
- [ ] Enter synchronizes the selected profile only after explicit selection.
- [x] A missing Destination file is created; a changed Destination file is replaced; an unchanged file is skipped.
- [x] Fast comparison uses length and `LastWriteTimeUtc` with a two-second tolerance and preserves Source `LastWriteTimeUtc` after copy.
- [x] Copying streams through a temporary file beside the Destination and leaves an existing Destination intact when staging fails.
- [ ] Extra files near the Destination are never deleted, and no synchronization-related watcher, poller, timer, or background service is introduced.
- [ ] The user receives a concise copied/unchanged/failed summary.

## Testing seam

Exercise the public core validate and synchronize operations against real temporary files. Assert resulting bytes, timestamps, unchanged behavior, old-Destination preservation after a staged failure, and structured counters. Do not test forwarding or source text.

## Demo path

Create a profile for one temporary configuration file, run `sy <profile>` in Flow, observe the first copy, edit the Source, observe replacement, then run again and observe an unchanged summary.

## Verification evidence

- Core suite: 12 passed, 0 failed, 0 skipped. The permanent locked-Destination regression uses FileShare.None and verifies a Failed result, original bytes, and staging-file cleanup.
- Solution build: 0 warnings and 0 errors after excluding nested projects from plugin compilation and awaiting the Flow AsyncAction.
- Real-filesystem runtime smoke: first copy, unchanged, changed replacement, timestamp preservation, and locked-target preservation passed.
- Installed Flow initialized SnapSync (startup log: 71 ms); actual `sy` query displayed the no-profiles/settings result through Windows UI Automation.
- Settings creation, save, reload after restart, profile fuzzy search, and Enter synchronization remain unverified. Result invocation and SendKeys did not open settings; SetForegroundWindow returned false. No additional deployment or restart is authorized.
