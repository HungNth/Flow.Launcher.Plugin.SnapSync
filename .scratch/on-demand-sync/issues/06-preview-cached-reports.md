# 06: Preview and cached Flow reports

**What to build:** A user can run a no-mutation Preview with Ctrl+Enter, inspect exact aggregate outcomes and prioritized details inside Flow Launcher, search the latest report, and then synchronize from freshly recomputed filesystem state.

**Blocked by:** 04: Multiple Destinations with independent outcomes; 05: Recursive directories, exclusions, and cloud-safe traversal.

**Parent specification:** [SnapSync On-Demand Synchronization](../spec.md)

**Status:** ready-for-agent

- [ ] Ctrl+Enter and the profile context action run Preview; Enter still runs synchronization.
- [ ] Preview executes the same discovery and comparison rules as synchronization but creates no directories, files, temporary files, or metadata changes.
- [ ] Reports distinguish Copied, WouldCopy, Unchanged, CreatedDirectory, WouldCreateDirectory, Excluded, Skipped, MissingSource, and Failed outcomes.
- [ ] Aggregate counters are exact and follow per-transfer versus Source-level counting rules.
- [ ] Only the latest Preview or synchronization report is retained, only in memory, and it is replaced after completion or cancellation.
- [ ] Cached detail is capped at 500 entries, prioritizes failures and actionable mutations, and explicitly marks truncation.
- [ ] `View last report` enters an internal `:report` Flow mode that fuzzy-filters cached path and status text without filesystem access.
- [ ] Profile context actions are Sync now, Preview changes, Edit profile, and View last report.
- [ ] Synchronizing from a Preview always reruns structural preflight and comparison rather than executing a cached plan.
- [ ] Notifications distinguish no changes, success, partial failure, configuration failure, and cancelled/partial completion where applicable.

## Testing seam

Invoke public Preview and synchronization APIs against the same real temporary trees. Assert zero Preview mutation, equivalent discovery, distinct would-versus-did outcomes, exact counters, fresh recomputation after filesystem drift, cache priority/truncation behavior, and report filtering as pure in-memory behavior.

## Demo path

Run Ctrl+Enter for a profile with changed, unchanged, excluded, and missing entries; filter the report in Flow; modify the Source after Preview; then run Sync now and observe the freshly recomputed final report.
