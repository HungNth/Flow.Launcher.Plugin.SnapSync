# SnapSync On-Demand Synchronization

**Status:** ready-for-agent

## Problem Statement

People who maintain configuration files, dotfiles, scripts, application settings, templates, and similar local data need a fast way to push selected content to backup or synchronization folders. Existing continuous synchronization tools are often broader than necessary, consume resources while idle, or introduce two-way and destructive behavior that is unsafe for authoritative configuration sources.

SnapSync currently exists only as a minimal Flow Launcher plugin shell. It does not yet provide reusable Sync Profiles, filesystem comparison, safe copying, Preview, settings management, structured reports, or the safety protections required for unattended use from a launcher.

## Solution

Build SnapSync as a lightweight C# Flow Launcher plugin for explicit, one-way synchronization from authoritative Sources to exact Destinations. A user invokes `sy`, selects a Sync Profile, and either synchronizes immediately or runs a Preview. The plugin performs no synchronization-related polling, watching, scanning, hashing, or copying while idle or while merely querying Flow Launcher.

The synchronization engine will live in a Flow-independent core library so the same behavior can later support a CLI. It will synchronize files and directories, preserve empty directories, support multiple Destinations, compare by fast metadata or SHA-256, apply relative glob exclusions, continue after recoverable entry failures, and return structured Synchronization Reports.

Safety is the default: no deletion or mirroring, no implicit path naming, no overlapping read/write areas, no navigation-link traversal, no direct overwrite of a good destination file, no automatic retries, and no execution of synchronized content.

## User Stories

1. As a Flow Launcher user, I want `sy` to list my Sync Profiles, so that I can start synchronization without opening another application.
2. As a Flow Launcher user, I want profile search to use Flow's fuzzy matching, so that abbreviated queries find the intended profile.
3. As a Flow Launcher user, I want `sy all` to target every enabled Sync Profile, so that I can update all configured Destinations in one explicit operation.
4. As a Flow Launcher user, I want disabled Sync Profiles to remain visible as disabled, so that I can find and edit them without accidentally running them.
5. As a Flow Launcher user, I want Enter to synchronize a selected profile, so that the primary action remains fast.
6. As a Flow Launcher user, I want Ctrl+Enter to run a Preview, so that I can inspect changes without mutating the filesystem.
7. As a Flow Launcher user, I want context actions for synchronization, Preview, editing, and the last report, so that related actions remain discoverable.
8. As a Flow Launcher user, I want Query handling to use only loaded configuration and cached report data, so that typing remains responsive.
9. As a Flow Launcher user, I want synchronization to start only after selecting an action, so that typing a query can never modify files.
10. As a user, I want named Sync Profiles, so that related configuration files can be synchronized together.
11. As a user, I want each Sync Profile to contain multiple Sync Items, so that one command can synchronize a coherent set of content.
12. As a user, I want each Sync Item to have one authoritative Source and one or more Destinations, so that the same content can be pushed to several backup locations.
13. As a user, I want profile names to be unique and item names to be unique within a profile, so that search and reports remain unambiguous.
14. As a user, I want to add, edit, rename, duplicate, reorder, enable, disable, and delete Sync Profiles and Sync Items, so that configuration can evolve safely.
15. As a user, I want file and folder pickers alongside editable paths, so that both browsing and direct path entry are convenient.
16. As a user, I want settings edits to be validated before saving, so that structural configuration errors cannot reach an operation.
17. As a user, I want missing network, removable, or Source paths to produce warnings rather than block saving, so that temporarily unavailable devices remain configured.
18. As a user, I want enabled configurations with overlapping read or write areas to be rejected, so that output never depends on execution order.
19. As a user, I want disabled configurations to remain storable when they conflict only with enabled alternatives, so that I can keep mutually exclusive setups.
20. As a user, I want settings to persist through Flow Launcher's JSON storage with a schema version, so that configuration is maintainable and future migrations are possible.
21. As a user, I want an operation to use an immutable configuration snapshot, so that settings changes during a run affect only future operations.
22. As a user, I want a file Source to map to an exact destination file path, so that destination naming is explicit.
23. As a user, I want a directory Source to map to an exact destination root, so that SnapSync never appends a Source basename implicitly.
24. As a user, I want `%ENVIRONMENT_VARIABLE%` paths and a leading `~` to expand at operation time, so that configuration remains portable across machines and accounts.
25. As a user, I want paths to be absolute after expansion, so that behavior does not depend on a process working directory.
26. As a user, I want duplicate and identical Source/Destination paths detected after normalization, so that SnapSync cannot copy onto itself.
27. As a user, I want recursive Source/Destination relationships rejected in both directions, so that synchronization cannot enumerate its own output or overwrite its input tree.
28. As a user, I want configured paths that traverse a symbolic link, junction, or mount point to be rejected, so that path boundaries cannot be bypassed through aliases.
29. As a user, I want regular cloud placeholders distinguished from navigation links, so that OneDrive and Dropbox content is not silently omitted.
30. As a user, I want online-only cloud files hydrated only when an explicit Preview or synchronization requires their contents, so that network activity remains user-triggered.
31. As a user, I want a Sync Item in Auto mode to detect whether its Source is a file or directory for each operation, so that temporarily unavailable Sources can still be configured.
32. As a user, I want explicit File and Directory modes to fail clearly when the Source type does not match, so that configuration mistakes are visible.
33. As a user, I want new destination files copied, so that missing backups are created.
34. As a user, I want changed destination files replaced from the authoritative Source even when the destination timestamp is newer, so that synchronization remains one-way.
35. As a user, I want unchanged files skipped, so that repeated runs avoid unnecessary writes.
36. As a user, I want extra destination files left untouched, so that normal synchronization is never destructive.
37. As a user, I want missing destination directories created during synchronization, so that setup does not require manual directory creation.
38. As a user, I want empty Source directories represented at each Destination, so that directory structure is preserved completely.
39. As a user, I want nested directory trees synchronized recursively, so that a directory Sync Item covers all eligible descendants.
40. As a user, I want navigation links inside a Source tree skipped and reported, so that traversal cannot escape the configured tree or loop indefinitely.
41. As a user, I want multiple Destinations processed independently, so that one unavailable Destination does not block another.
42. As a user, I want operations processed sequentially, so that disk behavior and reporting remain deterministic and safe.
43. As a user, I want Fast comparison to use file size and `LastWriteTimeUtc` with a two-second tolerance, so that FAT/exFAT timestamp granularity does not cause perpetual copying.
44. As a user, I want copied files to preserve the Source `LastWriteTimeUtc`, so that future Fast comparisons remain reliable.
45. As a user, I want SHA-256 comparison available per Sync Item, so that small important files can be compared by content.
46. As a user, I want equal SHA-256 content treated as Unchanged without silently rewriting metadata, so that an Unchanged result guarantees no mutation.
47. As a user, I want copy operations to stream data instead of loading whole files into memory, so that large files remain practical.
48. As a user, I want copying to stage data in a temporary file beside the Destination and replace only after success, so that interruption cannot corrupt a good destination file.
49. As a user, I want a Source that changes during copying to fail that transfer and preserve the old Destination, so that SnapSync never publishes an inconsistent snapshot knowingly.
50. As a user, I want no automatic I/O retries in the first release, so that launcher actions do not enter long or surprising retry loops.
51. As a user, I want exclusion patterns relative to the Source root, so that configurations remain portable.
52. As a user, I want exclusion patterns to support `*`, `?`, and `**` with `/` as the canonical separator, so that common files and subtrees can be omitted predictably.
53. As a user, I want exclusion matching to be case-insensitive on Windows, so that matching follows platform path semantics.
54. As a user, I want excluded directories pruned before expensive comparison, so that ignored subtrees do not consume unnecessary time.
55. As a user, I want Preview to perform the same discovery and comparison as synchronization without creating directories, copying files, or modifying metadata, so that its report is trustworthy.
56. As a user, I want synchronization launched from a cached Preview to revalidate and recompare current filesystem state, so that stale plans are never executed blindly.
57. As a user, I want a structured result for copied, would-copy, unchanged, created-directory, would-create-directory, excluded, skipped, missing-source, and failed outcomes, so that every relevant behavior is explainable.
58. As a user, I want transfer counters to be per Source–Destination outcome, so that multiple Destinations are represented accurately.
59. As a user, I want excluded and missing Source outcomes counted at Source level, so that they are not multiplied artificially by destination count.
60. As a user, I want exact aggregate counters even for very large trees, so that summaries remain accurate.
61. As a user, I want cached report details bounded to 500 prioritized entries, so that large operations do not consume unbounded memory.
62. As a user, I want a report to state when details were truncated, so that aggregate totals are not mistaken for the displayed subset.
63. As a user, I want the cached report searchable in Flow through an internal report view, so that failures and paths are easy to find.
64. As a user, I want only the latest Preview or synchronization report retained in memory, so that SnapSync does not create a history or privacy-sensitive report store.
65. As a user, I want concise notifications for success, no changes, partial failure, cancellation, and configuration failure, so that operation state is immediately visible.
66. As a user, I want every failure to include the relevant Source, Destination where applicable, category, and message, so that I can diagnose it.
67. As a user, I want recoverable failures isolated to the affected entry or Destination, so that unrelated transfers continue.
68. As a user, I want structural configuration errors detected before any write in the selected operation, so that known-invalid configurations cannot produce partial output.
69. As a user, I want a missing Source reported without aborting unrelated Sync Items, so that temporarily unavailable content does not stop a whole profile.
70. As a user, I want locked, read-only, permission-denied, unavailable-drive, network, path, hash, and unexpected I/O errors reported without crashing Flow Launcher.
71. As a user, I want only one Preview or synchronization active at a time, so that concurrent operations cannot compete for the same filesystem.
72. As a user, I want a new request to report that an operation is already active rather than starting in parallel, so that behavior remains predictable.
73. As a user, I want `sy` to expose a Cancel action while an operation is active, so that a long operation can be stopped explicitly.
74. As a user, I want cancellation to interrupt an in-progress stream, remove its temporary file when possible, preserve the old Destination, and return a partial report, so that cancellation is both responsive and safe.
75. As a user, I want synchronization diagnostics written through Flow Launcher's logging system only during explicit work, so that idle operation remains silent.
76. As a user, I want synchronized file contents never executed or interpreted, so that SnapSync remains a filesystem copy tool.
77. As a user, I want no background watchers, polling, scheduled synchronization, or synchronization timers, so that idle resource use remains minimal.
78. As a future CLI user, I want the synchronization engine independent from Flow Launcher and WPF, so that the same behavior can be reused without rewriting it.

## Implementation Decisions

- The default Flow Launcher action keyword is `sy`. `all` is a reserved public command. Internal report navigation uses a colon-prefixed mode; profile names cannot start with `:`.
- Flow query handling only parses input, fuzzy-searches loaded configuration, reads the in-memory active-operation state, and filters the in-memory last report. It never enumerates Source trees, hashes files, creates directories, or copies content.
- The system is split into a Flow-independent `SnapSync.Core` library, a Flow Launcher/WPF adapter, and an xUnit test project. The core library has no Flow Launcher or WPF dependency.
- The core's highest-level API exposes configuration validation, Preview, and synchronization operations. It accepts immutable configuration snapshots, cancellation, and progress reporting, and returns structured reports.
- Sync Profiles own identity, name, optional description, enabled state, and Sync Items. Sync Items own identity, name, Source, Destinations, item type, comparison mode, exclusions, and enabled state. There is no profile-level comparison or exclusion inheritance in the first release.
- Configuration is persisted with Flow Launcher's JSON storage using `schemaVersion: 1`. User saves are immediate. Unsupported or corrupt configuration must surface a safe error and must not be silently replaced with empty configuration.
- The settings surface is a custom WPF panel supporting profile and item management, destination management, path pickers, inline validation, enable/disable, duplication, and useful reordering. Destructive configuration deletion requires confirmation.
- Disabled profiles remain searchable but cannot synchronize; selecting one opens editing. Disabled items are omitted from operations. Cross-profile path conflicts are enforced only for enabled participants, while every saved object must remain internally valid.
- Paths are expanded and normalized for every operation. A leading `~` maps to the current user's home directory. Unresolved environment variables, non-rooted paths, duplicate Destinations, identical paths, recursive relationships, and enabled read/write overlaps are structural errors.
- Existing path ancestors are inspected for Navigation Links. Configured paths traversing a symbolic link, junction, or mount point are rejected. Cloud placeholders and other non-navigation reparse points remain valid.
- An operation performs structural preflight across the entire selected scope before writing. Filesystem availability is handled at runtime: missing Sources and unavailable Destinations are reportable outcomes, not save-time structural errors.
- Preview and synchronization share discovery and comparison behavior. Synchronization never executes a cached Preview plan; it performs fresh preflight and comparison.
- Profiles, Sync Items, Destinations, and file operations are processed sequentially in stable configured order. Parallel copy is not implemented.
- Directory traversal streams entries rather than materializing an entire tree. It preserves relative structure and empty directories, applies exclusions before comparison, and does not descend through Navigation Links.
- Cloud placeholders are treated as ordinary filesystem entries. Content is hydrated by normal reads only when comparison or copying needs the bytes; hydration failures are normal per-entry failures.
- Fast comparison uses length and `LastWriteTimeUtc` with an inclusive two-second equivalence window. SHA-256 comparison streams content. Equal hashes cause no metadata mutation.
- Successful copy preserves Source `LastWriteTimeUtc` only. Creation time, ACLs, and file attributes are not preserved deliberately in the first release.
- Copying writes to a uniquely named temporary file in the Destination directory, flushes and finalizes it, sets Source modification time, and replaces the target only after success. Failed or cancelled transfers attempt to remove only their own temporary files.
- Source length and modification time are captured before and after transfer. A change invalidates the staged transfer, leaves the old Destination intact, and returns a source-changed failure without retry.
- Runtime failures are categorized and attached to the relevant Source and optional Destination. Recoverable errors do not abort unrelated work. There are no automatic retries.
- Preview and synchronization use the same report model. File transfer outcomes are per Source–Destination pair; exclusions and missing Sources are Source-level outcomes. Operation state is completed or cancelled.
- The plugin retains only the latest report in memory. Aggregate counters are exact; detail storage is capped at 500 entries, prioritizing failures and actionable mutations over unchanged and excluded detail. Truncation is explicit. Every failure is also logged through Flow Launcher.
- Only one operation can run globally. Additional requests return a busy result. Flow exposes an explicit Cancel result backed by the active operation's cancellation source.
- Enter synchronizes; Ctrl+Enter previews. Profile context actions are Sync, Preview, Edit, and View last report. The report view fuzzy-filters cached path and status text without filesystem access.
- The source remains authoritative. Changed Destinations are replaced regardless of which side has the newer timestamp. Extra Destination files and directories are never deleted.

## Testing Decisions

- The primary automated testing seam is the highest-level public API of `SnapSync.Core`: configuration validation, Preview, and synchronization.
- Tests use real temporary directories and files so Windows filesystem behavior, timestamps, locking, directory creation, atomic replacement, and path normalization are exercised rather than mocked.
- Progress reporting provides a deterministic synchronization point for cancellation and source-mutation scenarios without adding test-only hooks.
- Test assertions target consumer-visible behavior: resulting filesystem state, absence of mutation in Preview, preserved old Destinations after failure/cancellation, structured outcomes, aggregate counts, and validation errors.
- Data-driven xUnit tests cover path expansion and normalization, comparison modes, timestamp tolerance boundaries, exclusion patterns, type detection, and structural overlap combinations.
- Directory scenarios cover nesting, empty directories, exclusions, extra Destination entries, navigation links, cloud-compatible non-link reparse handling where the environment permits it, and unavailable descendants.
- Multiple-Destination scenarios cover all-success, mixed success, all-failure, and per-transfer counting.
- Safety scenarios cover same path, both containment directions, cross-item read/write overlap, temporary-file cleanup, locked/read-only Destinations, cancellation during streaming, and Source mutation during transfer.
- Flow Launcher adapter and WPF behavior are not validated with forwarding or source-text unit tests. They are verified through build/publish checks and an actual Flow Launcher smoke run covering plugin loading, `sy`, fuzzy profile search, Preview, synchronization, cancellation visibility, report navigation, and settings persistence.
- Permanent tests must remain deterministic, isolated, order-independent, and safe for the full suite. OS-specific cases that cannot be made deterministic are verified through targeted runtime smoke scenarios rather than brittle mocks.

## Out of Scope

- Two-way synchronization and conflict resolution between editable copies.
- Mirror mode, deletion, purge, or any automatic removal of extra Destination content.
- Continuous file watching, polling, scheduled synchronization, background synchronization services, or synchronization timers.
- Provider-specific Dropbox, OneDrive, Syncthing, or cloud APIs.
- Import/export and portable configuration files selected by the user.
- Sync history, profile history, statistics, previous-version restore, and persisted reports.
- Robocopy or alternative copy engines.
- Parallel file or Destination copying.
- Configurable retries or long retry loops.
- Copying or following symbolic links, junctions, and mount points.
- Preserving creation times, ACLs, alternate streams, or arbitrary file attributes.
- Automatic metadata normalization when SHA-256 content is unchanged.
- A standalone CLI executable. The core boundary supports one later.
- Packaging, release automation, Plugin Store submission, and localization unless separately requested.

## Further Notes

- The first complete release is complete only when the approved v1 behavior and acceptance criteria are met end to end. Tickets may deliver smaller working vertical slices, but partial slices are not a substitute for the full release contract.
- Established-tool research supports additive update semantics, two-second timestamp tolerance, timestamp preservation, fail-safe temporary-file replacement, and explicit protection against recursive Source/Destination relationships.
- Windows Cloud Files research shows that `FileAttributes.ReparsePoint` alone cannot identify symbolic links or junctions. SnapSync must distinguish Navigation Links from OneDrive and Dropbox placeholders so that cloud-backed files are not silently omitted.
- Research evidence is recorded alongside this specification under the feature's research notes.
