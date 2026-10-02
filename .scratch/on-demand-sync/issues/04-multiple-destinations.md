# 04: Multiple Destinations with independent outcomes

**What to build:** A user can attach several exact Destinations to one Sync Item and receive an independent outcome for every Source–Destination transfer, including mixed success when one location is unavailable.

**Blocked by:** 03: Profile catalog and Sync All.

**Parent specification:** [SnapSync On-Demand Synchronization](../spec.md)

**Status:** resolved

- [x] The item editor supports adding, editing, removing, inspecting, and usefully reordering multiple Destinations.
- [x] Duplicate normalized Destinations and overlapping Destination write areas are rejected before save or execution.
- [x] Each Destination is compared and processed independently in stable configured order.
- [x] Failure at one Destination does not prevent remaining independent Destinations from being processed.
- [x] Copied, unchanged, and failed counts are per Source–Destination outcome.
- [x] Missing Source and excluded outcomes remain Source-level and are not multiplied by Destination count.
- [x] Summary and detail results identify the affected Destination when applicable.
- [x] Fail-safe temporary replacement is applied independently in each Destination directory.
- [x] A Destination that is offline, locked, read-only, or otherwise unavailable remains configured after failure.
- [x] Settings and Flow subtitles make Destination counts easy to inspect.

## Testing seam

Run the public core synchronization API against two or more real temporary Destinations. Cover all-success, one-fails, all-fail, mixed unchanged/copied states, duplicate validation, and exact per-transfer counters.

## Demo path

Configure one Source file with two Destinations, synchronize both, make one Destination unavailable, edit the Source, and observe one successful transfer plus one isolated failure.

## Verification evidence

- Core suite: 52 passed, 0 failed, 0 skipped. `MultipleDestinationsTests` covers all-success across multiple destinations, independent outcome when one destination is locked while others succeed, missing-source outcome counted once at source level without multiplication, and mixed unchanged/copied destinations.
- Solution build: 0 warnings, 0 errors.
- Standalone runtime smoke: executed real `SyncEngine` synchronously (`pwsh -NoProfile -File .scratch/ticket04-smoke.ps1`), verified 1 locked failure + 2 copied destinations, destination file integrity, and source-level failure count on missing source. Stdout: "PASS: Ticket 04 runtime smoke test verified synchronously.", exit code 0.
- WPF Settings UI implemented: `SelectedItem.Destinations` observable collection with inline rows: `[ Path TextBox ] [ Browse... ] [ ▲ ] [ ▼ ] [ Remove ]` and `+ Add Destination`. Browse button placed on the same row with the path field per user request, removing disconnected duplicate controls. Destination count displayed in sync item summary (`({0} destination(s))`).
- Flow UI acceptance: user confirmed live Flow Launcher verification. Multiple destinations can be added, browsed, reordered, removed, saved, and executed independently. Restarted cleanly with settings SHA-256 intact.
- Commits: no new commits created, maintaining compliance with AGENTS.md.

## Answer

Ticket 04 is complete. A Sync Item now supports multiple exact destinations, validated for duplicates and overlaps, processed sequentially and independently. One destination failure or lock does not prevent other destinations from completing, and missing-source failures remain accurately counted at the Source level. The settings interface presents an inline, single-row destination editor with per-row Browse, reorder, and remove controls. All 52 unit tests pass, compiler build has 0 warnings and 0 errors, synchronous runtime smoke passes with exit code 0, and user manual acceptance verified the live UI in Flow Launcher.
