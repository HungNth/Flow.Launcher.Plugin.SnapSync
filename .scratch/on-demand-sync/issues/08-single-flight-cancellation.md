# 08: Single-flight operation and responsive cancellation

**What to build:** A user can run only one Preview or synchronization at a time, see that operation from Flow, cancel it while a file is streaming, and receive a safe partial report without corrupting an existing Destination.

**Blocked by:** 06: Preview and cached Flow reports.

**Parent specification:** [SnapSync On-Demand Synchronization](../spec.md)

**Status:** ready-for-agent

- [ ] The plugin owns one global active-operation state covering Preview and synchronization.
- [ ] A request made while busy returns a clear busy result and never starts concurrent filesystem work.
- [ ] `sy` exposes a Cancel current operation result while work is active.
- [ ] Cancellation propagates through discovery, hashing, and copy streams and stops new entry work.
- [ ] Cancelling an in-progress staged copy removes its temporary file when possible and leaves the old Destination intact.
- [ ] A cancelled operation returns exact partial counters, retained prioritized details, and operation state Cancelled.
- [ ] Each operation uses an immutable configuration snapshot captured at start; settings saved during a run apply only to later operations.
- [ ] Source length and modification time are checked before and after transfer; a change discards the staged file and reports a source-changed failure without retry.
- [ ] No automatic retry is introduced for cancellation, source mutation, locks, unavailable drives, or other I/O failures.
- [ ] Flow remains responsive while long operations execute.

## Testing seam

Use public progress reporting as a deterministic synchronization point in real-temp tests. Cancel after a copy has started, mutate a Source mid-transfer, and assert prompt completion, old-Destination preservation, temporary-file cleanup, partial report correctness, and rejection of a concurrent operation.

## Demo path

Start synchronization of a large file, reopen Flow with `sy`, select Cancel current operation, and observe a Cancelled report while the pre-existing Destination remains valid.
