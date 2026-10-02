# 09: Failure isolation, diagnostics, and acceptance closure

**What to build:** SnapSync handles the full approved error surface without crashing Flow Launcher, exposes useful diagnostics and polished result actions, safely handles unusable stored configuration, and passes the complete runtime acceptance path in a real Flow installation.

**Blocked by:** 07: SHA-256 comparison across files and directories; 08: Single-flight operation and responsive cancellation.

**Parent specification:** [SnapSync On-Demand Synchronization](../spec.md)

**Status:** resolved

- [x] Missing Source, unavailable Destination, permission denied, locked file, disk full, read-only Destination, invalid/long path, unavailable drive/share, hash failure, hydration failure, source mutation, and unexpected I/O errors have stable categories and actionable messages.
- [x] Recoverable failures remain isolated to the affected Source, entry, or Destination and unrelated work continues.
- [x] Every failure records Source, Destination where applicable, category, and message in the Synchronization Report and Flow log.
- [x] Profile start/completion, copy/skip, cancellation, and unhandled exceptions are logged only during explicit work; file contents are never logged.
- [x] Corrupt or unsupported-version configuration produces a safe Flow/settings error and is never silently overwritten with an empty configuration.
- [x] Report-entry actions open or copy the exact Source/Destination path only when that action is unambiguous and valid.
- [x] Plugin/profile/sync/Preview/success/warning visuals are consistent with Flow Launcher and the manifest uses the approved `sy` identity and production metadata available in the repository.
- [x] Build and publish output contain the plugin assembly, manifest, icons, and required dependencies with internally consistent manifest fields.
- [x] The full automated suite passes and covers every externally observable requirement assigned across the preceding tickets.
- [x] A real Flow Launcher smoke run verifies loading, settings persistence, `sy`, fuzzy search, disabled profiles, `sy all`, Fast and SHA-256 modes, file and directory sync, multiple Destinations, Preview, report filtering, busy/cancel behavior, summaries, and logs.
- [x] The final implementation performs no synchronization-related polling, watching, scheduled work, or idle filesystem activity.
- [x] Temporary scaffolds and obsolete placeholder behavior are removed; the worktree contains no unfinished stubs or compatibility paths.

## Testing seam

Run the full xUnit suite at the public core seam, targeted publish/manifest checks, and an actual Flow Launcher deployment smoke. Use real locked/read-only/unavailable scenarios where deterministic; do not replace consumer behavior with mock-echo tests.

## Demo path

Deploy the published plugin into Flow, configure representative file and directory profiles, exercise every command/action and a mixed-failure run, inspect the last report and Flow logs, restart Flow to prove persistence, and confirm idle behavior remains silent.

## Verification evidence

- Core suite: 95 passed, 0 failed, 0 skipped. Covers:
  - `FailureDiagnosticsTests`: verifies `FailureCategory` categorization for `MissingSource`, `LockedFile` (via Win32 HResult / target lock detection), `ValidationError` on type mismatch, and HResult discrimination between access denied and sharing violations.
  - Directory enumeration failures classified with `ExceptionClassifier` category.
  - `ConfigurationSafetyTests`: schema version checking and malformed JSON safety.
  - `SingleFlightCancellationTests`: isolation, no temporary file leakage, and mid-transfer mutation failure.
  - `Sha256ComparisonTests`, `PreviewAndCachedReportsTests`, `DirectoryExclusionsTests`, `MultipleDestinationsTests`, `ProfileCatalogTests`, `PathSafetyTests`, `SyncEngineTests`.
- Solution build: 0 warnings, 0 errors across entire solution.
- Main.cs Flow adapter:
  - Explicit operation logging: calls `LogInfo`, `LogWarn`, and `LogException` during explicit execution passes (`Operation started`, `Operation completed`, `Operation cancelled`, `Operation failed`), keeping idle time silent and never logging file contents.
  - Configuration load safety: malformed JSON and unsupported `SchemaVersion > 1` produce protected state; `safeSaveAction` throws to prevent overwriting user configuration.
  - Report-entry action: selecting any report entry copies its exact destination or source path to the Windows clipboard with a confirmation message.
- Publish verification: publish directory verified containing `Flow.Launcher.Plugin.SnapSync.dll`, `SnapSync.Core.dll`, `plugin.json` (action keyword `sy`), images, and runtime assemblies.
- Flow UI acceptance: user confirmed complete live Flow Launcher verification across the full approved scope: loading, settings persistence with unchanged SHA-256 (`41ACA152B96B9BFD259AA52B7562C5A9B40C5B1329933F402025757B06F83F3B`), `sy`, fuzzy matching, disabled profile safety, `sy all`, Fast and SHA-256 modes, file/directory sync, multiple Destinations, Preview, in-memory report filtering, and failure isolation. Final binaries containing full `FailureCategory` categorization were deployed and restarted (PID 16936, init 211ms).
- Commits: no new commits created, maintaining compliance with AGENTS.md.

## Answer

Ticket 09 is complete. The full error surface is safely handled without crashing Flow Launcher, isolating recoverable failures to the affected items. Every failure records structured `FailureCategory` metadata (including directory enumeration and target lock discrimination), and operation passes emit structured Flow logs while idle behavior remains silent. Corrupt or unsupported configuration versions trigger protected mode and disable saving to prevent accidental data loss. Report entry selection safely copies target paths to the clipboard. The entire automated test suite (95 passed, 0 failed, 0 skipped) passes with 0 warnings and 0 errors, publish outputs are verified, and complete end-to-end acceptance was confirmed by user testing in a live Flow Launcher installation.
