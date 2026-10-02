# 06: Preview and cached Flow reports

**What to build:** A user can run a no-mutation Preview with Ctrl+Enter, inspect exact aggregate outcomes and prioritized details inside Flow Launcher, search the latest report, and then synchronize from freshly recomputed filesystem state.

**Blocked by:** 04: Multiple Destinations with independent outcomes; 05: Recursive directories, exclusions, and cloud-safe traversal.

**Parent specification:** [SnapSync On-Demand Synchronization](../spec.md)

**Status:** resolved

- [x] Ctrl+Enter and the profile context action run Preview; Enter still runs synchronization.
- [x] Preview executes the same discovery and comparison rules as synchronization but creates no directories, files, temporary files, or metadata changes.
- [x] Reports distinguish Copied, WouldCopy, Unchanged, CreatedDirectory, WouldCreateDirectory, Excluded, Skipped, MissingSource, and Failed outcomes.
- [x] Aggregate counters are exact and follow per-transfer versus Source-level counting rules.
- [x] Only the latest Preview or synchronization report is retained, only in memory, and it is replaced after completion or cancellation.
- [x] Cached detail is capped at 500 entries, prioritizes failures and actionable mutations, and explicitly marks truncation.
- [x] `View last report` enters an internal `:report` Flow mode that fuzzy-filters cached path and status text without filesystem access.
- [x] Profile context actions are Sync now, Preview changes, Edit profile, and View last report.
- [x] Synchronizing from a Preview always reruns structural preflight and comparison rather than executing a cached plan.
- [x] Notifications distinguish no changes, success, partial failure, configuration failure, and cancelled/partial completion where applicable.

## Testing seam

Invoke public Preview and synchronization APIs against the same real temporary trees. Assert zero Preview mutation, equivalent discovery, distinct would-versus-did outcomes, exact counters, fresh recomputation after filesystem drift, cache priority/truncation behavior, and report filtering as pure in-memory behavior.

## Demo path

Run Ctrl+Enter for a profile with changed, unchanged, excluded, and missing entries; filter the report in Flow; modify the Source after Preview; then run Sync now and observe the freshly recomputed final report.

## Verification evidence

- Core suite: 80 passed, 0 failed, 0 skipped. `PreviewAndCachedReportsTests` covers:
  - Preview zero filesystem mutation (no directories, files, or temporary artifacts created).
  - Accurate distinction between would-versus-did outcomes (`WouldCopy`, `WouldCreateDirectory`).
  - Fresh recomputation upon subsequent synchronization after source mutation.
  - Capped 500-entry memory limit prioritizing actionable entries and failures with exact aggregate counters and `IsTruncated` flag.
- Solution build: 0 warnings, 0 errors.
- Standalone runtime smoke: executed real `SyncEngine.PreviewAsync` and `SynchronizeAsync` synchronously (`pwsh -NoProfile -File .scratch/ticket06-smoke.ps1`), verified zero preview mutation and freshly synced content. Stdout: "PASS: Ticket 06 runtime smoke test verified synchronously.", exit code 0.
- Main.cs Flow adapter:
  - Supports Ctrl+Enter (via `SpecialKeyState.CtrlPressed`) for non-mutating Preview.
  - Implements `IContextMenu` with 4 actions: "Sync now", "Preview changes", "Edit profile", and "View last report".
  - Implements internal `:report` mode (`QueryReportMode`) with pure in-memory fuzzy filtering of cached entries.
- Flow UI acceptance: user confirmed live Flow Launcher verification. Ctrl+Enter executes no-mutation Preview, Enter runs real sync, Context Menu options work (Sync now, Preview changes, Edit profile, View last report) with "Sync now" hidden on disabled profiles, and `sy :report` fuzzy filters in-memory report entries. Settings SHA-256 intact.
- Commits: no new commits created, maintaining compliance with AGENTS.md.

## Answer

Ticket 06 is complete. Non-mutating preview (`PreviewAsync`) shares full discovery, comparison, and preflight logic with synchronization while causing zero filesystem mutations. Reports accurately distinguish `WouldCopy`, `WouldCreateDirectory`, `MissingSource`, and `Excluded` outcomes. In-memory caching retains only the latest report, capping detail at 500 prioritized items with exact aggregate counters and truncation flagging. Live Flow integration supports Ctrl+Enter previewing, IContextMenu profile actions, and `sy :report` in-memory report navigation. All 80 unit tests pass, compiler build has 0 warnings and 0 errors, and user manual acceptance verified all actions in live Flow Launcher.
