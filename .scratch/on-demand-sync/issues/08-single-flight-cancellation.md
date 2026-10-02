# 08: Single-flight operation and responsive cancellation

**What to build:** A user can run only one Preview or synchronization at a time, see that operation from Flow, cancel it while a file is streaming, and receive a safe partial report without corrupting an existing Destination.

**Blocked by:** 06: Preview and cached Flow reports.

**Parent specification:** [SnapSync On-Demand Synchronization](../spec.md)

**Status:** resolved

- [x] The plugin owns one global active-operation state covering Preview and synchronization.
- [x] A request made while busy returns a clear busy result and never starts concurrent filesystem work.
- [x] `sy` exposes a Cancel current operation result while work is active.
- [x] Cancellation propagates through discovery, hashing, and copy streams and stops new entry work.
- [x] Cancelling an in-progress staged copy removes its temporary file when possible and leaves the old Destination intact.
- [x] A cancelled operation returns exact partial counters, retained prioritized details, and operation state Cancelled.
- [x] Each operation uses an immutable configuration snapshot captured at start; settings saved during a run apply only to later operations.
- [x] Source length and modification time are checked before and after transfer; a change discards the staged file and reports a source-changed failure without retry.
- [x] No automatic retry is introduced for cancellation, source mutation, locks, unavailable drives, or other I/O failures.
- [x] Flow remains responsive while long operations execute.

## Testing seam

Use public progress reporting as a deterministic synchronization point in real-temp tests. Cancel after a copy has started, mutate a Source mid-transfer, and assert prompt completion, old-Destination preservation, temporary-file cleanup, partial report correctness, and rejection of a concurrent operation.

## Demo path

Start synchronization of a large file, reopen Flow with `sy`, select Cancel current operation, and observe a Cancelled report while the pre-existing Destination remains valid.

## Verification evidence

- Core suite: 88 passed, 0 failed, 0 skipped. `SingleFlightCancellationTests` covers:
  - Immediate cancellation returning a partial report with `IsCancelled == true`, preserving original destination, and cleaning up temporary files.
  - Deterministic mid-stream cancellation via internal testing seam: copies first item, cancels, retains exact partial counters (`Copied: 1`, `IsCancelled: true`), leaves second destination untouched, and cleans all temp files.
  - Pre-replacement source stability checks: discards staged copy and reports failure (`Failed: 1`) without retry when source mutates mid-transfer, leaving old destination intact.
- Solution build: 0 warnings, 0 errors.
- Standalone runtime smoke: executed real `SyncEngine` synchronously (`pwsh -NoProfile -File .scratch/ticket08-smoke.ps1`), verified `IsCancelled == true`, zero destination mutation, and complete temporary file cleanup. Stdout: "PASS: Ticket 08 public cancellation smoke verified synchronously.", exit code 0.
- Main.cs Flow adapter:
  - Single-flight concurrency: `_operationLock`, `_activeCts`, and `_activeOperationDescription` ensure only one operation runs globally.
  - Requests made while busy display a busy banner with score 2000 and offer a direct "Cancel current operation" result with score 1900.
  - Immutable configuration snapshot: `CloneConfiguration` and `CloneProfile` capture immutable deep copies at start so settings mutations during an operation never affect the running pass.
- Flow UI acceptance: user confirmed live Flow Launcher verification. Busy banner appears when an operation runs, "Cancel current operation" terminates in-progress runs safely, and temporary files are discarded without corrupting destinations.
- Commits: no new commits created, maintaining compliance with AGENTS.md.

## Answer

Ticket 08 is complete. Global single-flight concurrency guarantees that only one Preview or synchronization operation runs at any time, returning a busy banner for concurrent requests. An explicit Cancel result triggers immediate cooperative cancellation through copy and hash streams, cleans up temporary files, leaves existing destinations intact, and retains exact partial reports with IsCancelled: true. Pre-replacement source stability checks detect mid-transfer source mutations and report failures without retry. Immutable configuration snapshots ensure run consistency. All 88 unit tests pass, compiler build has 0 warnings and 0 errors, and user manual acceptance verified the live UI in Flow Launcher.
