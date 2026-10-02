# 02: Portable and conflict-safe paths

**What to build:** A user can configure portable Source and Destination paths while SnapSync blocks ambiguous, recursive, aliased, or order-dependent path relationships before any write. Runtime path behavior is the same in settings validation, Preview, and synchronization.

**Blocked by:** 01: One-file Fast synchronization end to end.

**Parent specification:** [SnapSync On-Demand Synchronization](../spec.md)

**Status:** resolved

- [x] A leading `~` and Windows environment variables expand at operation time for Sources and Destinations.
- [x] Expanded paths must be rooted drive or UNC paths; unresolved variables and relative paths are validation errors.
- [x] File Destinations are exact target files and directory Destinations are exact target roots; SnapSync never appends a Source basename implicitly.
- [x] Normalization is case-insensitive on Windows and detects duplicate Destinations and identical Source/Destination paths.
- [x] Both Source-inside-Destination and Destination-inside-Source relationships are rejected.
- [x] Enabled write/write and read/write overlaps are detected across the configuration so execution order cannot determine final output.
- [x] Existing path ancestors that are symbolic links, junctions, or mount points are rejected, while non-navigation cloud placeholders remain valid.
- [x] Structural validation covers the entire selected operation and aborts before all writes when any structural error exists.
- [x] Missing Sources and temporarily unavailable network/removable paths remain saveable warnings and become runtime outcomes rather than structural save failures.
- [x] Settings show actionable inline messages for every rejected path relationship.

## Testing seam

Use data-driven tests through the public core validation and operation APIs. Cover environment expansion, home expansion, unresolved variables, UNC paths, case variants, containment in both directions, cross-item overlaps, and navigation-link ancestors with real temporary filesystem objects where supported.

## Demo path

Configure a portable `%USERPROFILE%` Source and a valid Destination, then demonstrate save-time rejection for same-path, recursive, and junction-aliased variants without any Destination mutation.

## Verification evidence

- Integrated core suite: 39 passed, 0 failed, 0 skipped. Public-API regressions cover relative and drive-relative rejection, home expansion, Source/Destination environment re-expansion between operations, UNC paths, case variants, overlap/disabled rules, and structural no-write preflight. A literal percent character in a filename remains valid.
- Solution build: 0 warnings, 0 errors after disambiguating an XML documentation reference.
- Real runtime smoke passed portable copy/content, same-path rejection, selected-scope no-write preflight, cross-profile read/write conflict, disabled alternative, actual Windows junction ancestor rejection, and saveable missing-Source warnings. The final run completed with exit code 0 and removed its owned fixture directory.
- Settings use candidate validation before configuration mutation, show all structural messages inline, and retain raw portable expressions. The one-profile Flow action relies on the core selected-profile preflight, not whole-catalog runtime validation.
- User confirmed actual Flow Settings UI behavior: identical/containment path configurations are blocked with inline errors and preserve existing configuration without writing; portable path `~\\.omp\\agent\\config.yml` saved successfully.
- Persisted JSON verified at `%APPDATA%\FlowLauncher\Settings\Plugins\SnapSync.Core\SnapSyncConfiguration.json`: schema version 1, retaining raw portable token `~\\.omp\\agent\\config.yml` without premature expansion.
- Navigation checks distinguish LinkTarget from arbitrary reparse metadata; this machine's smoke used a real junction, not a cloud-provider placeholder. Cloud-provider hydration/traversal acceptance belongs to ticket 05.
- Work remains uncommitted under the user's latest instruction. Existing ticket-01 commits 9075ddf and f9d93d0 are unchanged; no history rewrite or further commit is authorized.

## Answer

Ticket 02 is complete. Portable path expansion (`~`, `%ENV%`), safe normalization, preflight containment and overlap checks, junction ancestor rejection, and nonblocking availability warnings are fully implemented in the core engine and integrated into the Flow plugin settings view and runtime actions. All 39 unit tests pass, compiler build has 0 warnings and 0 errors, standalone runtime smoke passes, and user manual acceptance verified inline settings validation and raw portable expression persistence.
