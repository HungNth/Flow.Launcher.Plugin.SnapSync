# 03: Profile catalog and Sync All

**What to build:** A user can manage a real catalog of Sync Profiles and Sync Items, search enabled or disabled profiles, run one profile, or run every enabled profile through `sy all` in stable sequential order.

**Blocked by:** 02: Portable and conflict-safe paths.

**Parent specification:** [SnapSync On-Demand Synchronization](../spec.md)

**Status:** resolved

- [x] Settings support adding, renaming, editing, duplicating, deleting, enabling, disabling, and reordering Sync Profiles and Sync Items.
- [x] Profile names are trimmed and unique case-insensitively across the configuration; item names are unique case-insensitively within a profile.
- [x] `all` is reserved and profile names cannot begin with `:`; duplicate actions generate an unambiguous copy name.
- [x] Every saved object remains internally valid; cross-profile path conflicts block save or enable only when all conflicting participants are enabled.
- [x] `sy` lists enabled profiles and visibly disabled profiles with useful item-count subtitles.
- [x] Selecting a disabled profile opens editing and cannot synchronize it as a one-time exception.
- [x] `sy all` runs all enabled profiles, skips disabled profiles and items, and aggregates a deterministic summary.
- [x] Profiles and items run sequentially in configured order; no parallel copy behavior is added.
- [x] Delete operations require confirmation, and saved catalog changes survive Flow restart.
- [x] The no-profile state offers a direct path to create the first profile.

## Testing seam

Test catalog validation and `Sync All` through public core operations with multiple temporary Sources. Assert unique-name rules, disabled omission, stable aggregate outcomes, structural preflight, and that unrelated enabled profiles synchronize in configured order.

## Demo path

Create two enabled profiles and one disabled profile, fuzzy-search each by abbreviation, run one profile, then run `sy all` and observe only the enabled profiles in the aggregate summary.

## Verification evidence

- Core suite: 48 passed, 0 failed, 0 skipped. `ProfileCatalogTests` verifies `all` reservation, colon rejection, duplicate profile names across config, duplicate item names in a profile, sequential execution in configured order skipping disabled profiles, and catalog preflight aborting without partial writes.
- Solution build: 0 warnings, 0 errors.
- Standalone runtime smoke: executed real `SyncEngine` synchronously (`pwsh -NoProfile -File .scratch/ticket03-smoke.ps1`), verified name validation ('all', ':report', duplicate profiles), sequential execution of enabled profiles, skipping disabled profiles, and file content integrity. Stdout: "PASS: Ticket 03 runtime smoke test verified synchronously.", exit code 0.
- WPF Settings Catalog View/ViewModel implemented: full catalog master-detail UI, Add/Duplicate/Delete/Move Up/Move Down for profiles and items, delete confirmation dialogs, and unambiguous duplicate names (`(Copy)`).
- Main.cs adapter: `sy all` query recognized with score 1000, calling `SynchronizeAllAsync` sequentially; disabled profiles open Settings dialog without synchronization.
- Flow UI acceptance: user confirmed actual Flow Launcher behavior. Catalog management (Add/Duplicate/Delete/Reorder/Enable/Disable) and `sy all` executed cleanly and as expected. Flow restarted cleanly (PID 39704, plugin init in 150 ms) preserving settings SHA-256 (`EC3F314C7AC5380584CB5EFAFB31E5B8CC34F456ED94A49AA11BB9B8309CA9E5`).
- Commits: no new commits created, maintaining compliance with AGENTS.md.

## Answer

Ticket 03 is complete. The profile catalog architecture supports full CRUD, duplication with unambiguous copy names, reordering, enable/disable states, case-insensitive unique names, reservation of 'all' and rejection of ':'-prefixed profile names. Sequential `SynchronizeAllAsync` preflights the whole catalog before writes and executes enabled profiles in deterministic order while skipping disabled entries. All 48 unit tests pass, compiler build has 0 warnings and 0 errors, synchronous runtime smoke passes with exit code 0, and user manual acceptance verified all UI and `sy all` operations in live Flow Launcher.
