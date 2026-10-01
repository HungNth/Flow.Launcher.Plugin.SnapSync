# 09: Failure isolation, diagnostics, and acceptance closure

**What to build:** SnapSync handles the full approved error surface without crashing Flow Launcher, exposes useful diagnostics and polished result actions, safely handles unusable stored configuration, and passes the complete runtime acceptance path in a real Flow installation.

**Blocked by:** 07: SHA-256 comparison across files and directories; 08: Single-flight operation and responsive cancellation.

**Parent specification:** [SnapSync On-Demand Synchronization](../spec.md)

**Status:** ready-for-agent

- [ ] Missing Source, unavailable Destination, permission denied, locked file, disk full, read-only Destination, invalid/long path, unavailable drive/share, hash failure, hydration failure, source mutation, and unexpected I/O errors have stable categories and actionable messages.
- [ ] Recoverable failures remain isolated to the affected Source, entry, or Destination and unrelated work continues.
- [ ] Every failure records Source, Destination where applicable, category, and message in the Synchronization Report and Flow log.
- [ ] Profile start/completion, copy/skip, cancellation, and unhandled exceptions are logged only during explicit work; file contents are never logged.
- [ ] Corrupt or unsupported-version configuration produces a safe Flow/settings error and is never silently overwritten with an empty configuration.
- [ ] Report-entry actions open or copy the exact Source/Destination path only when that action is unambiguous and valid.
- [ ] Plugin/profile/sync/Preview/success/warning visuals are consistent with Flow Launcher and the manifest uses the approved `sy` identity and production metadata available in the repository.
- [ ] Build and publish output contain the plugin assembly, manifest, icons, and required dependencies with internally consistent manifest fields.
- [ ] The full automated suite passes and covers every externally observable requirement assigned across the preceding tickets.
- [ ] A real Flow Launcher smoke run verifies loading, settings persistence, `sy`, fuzzy search, disabled profiles, `sy all`, Fast and SHA-256 modes, file and directory sync, multiple Destinations, Preview, report filtering, busy/cancel behavior, summaries, and logs.
- [ ] The final implementation performs no synchronization-related polling, watching, scheduled work, or idle filesystem activity.
- [ ] Temporary scaffolds and obsolete placeholder behavior are removed; the worktree contains no unfinished stubs or compatibility paths.

## Testing seam

Run the full xUnit suite at the public core seam, targeted publish/manifest checks, and an actual Flow Launcher deployment smoke. Use real locked/read-only/unavailable scenarios where deterministic; do not replace consumer behavior with mock-echo tests.

## Demo path

Deploy the published plugin into Flow, configure representative file and directory profiles, exercise every command/action and a mixed-failure run, inspect the last report and Flow logs, restart Flow to prove persistence, and confirm idle behavior remains silent.
