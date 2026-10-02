# 05: Recursive directories, exclusions, and cloud-safe traversal

**What to build:** A user can synchronize directory trees, including empty directories, while exclusions prune unwanted content and navigation links cannot escape the configured Source. Cloud placeholders remain eligible and hydrate only when explicit work needs their contents.

**Blocked by:** 03: Profile catalog and Sync All.

**Parent specification:** [SnapSync On-Demand Synchronization](../spec.md)

**Status:** resolved

- [x] Sync Items support Auto, File, and Directory modes; Auto detects the current Source type for each operation.
- [x] A missing Auto Source reports MissingSource, and an explicit File/Directory type mismatch reports a clear failure.
- [x] Directory synchronization recursively preserves relative structure and creates missing Destination roots and empty directories.
- [x] Additional files and directories already present only at a Destination remain untouched.
- [x] Exclusion patterns are relative to the Source root, canonicalize separators to `/`, match case-insensitively, and support `*`, `?`, and `**`.
- [x] Excluded directories are pruned before comparison and produce one Excluded outcome rather than outcomes for every descendant.
- [x] Nested symbolic links, junctions, and mount points are not followed and produce Skipped outcomes.
- [ ] Non-navigation reparse points, including supported OneDrive and Dropbox placeholders, are treated as ordinary entries.
- [ ] Online-only content hydrates only when hashing or copying needs bytes; hydration failure is isolated and reported.
- [x] Traversal streams work and results without constructing an unnecessary full in-memory directory tree.

## Testing seam

Use real temporary directory trees through the public core APIs. Cover nesting, empty directories, exclusions and pruning, extra Destination entries, Auto detection, explicit type mismatch, navigation-link skipping, and cloud-compatible reparse classification where the host environment permits deterministic setup.

## Demo path

Configure a directory containing nested files, an empty directory, an excluded cache subtree, and a navigation link. Synchronize it and verify the expected tree, untouched Destination extras, excluded subtree, and Skipped link outcome.

## Verification evidence

- Core suite: 74 passed, 0 failed, 0 skipped.
  - `DirectoryExclusionsTests`: recursive tree synchronization, empty directory creation, extra destination files preserved, Auto detection for files and directories, and explicit File/Directory mismatch errors.
  - `ExclusionPatternTests`: wildcard `*`, single-character `?`, recursive `**` (e.g. `docs/**/*.md`), prefix directory pruning (`foo/**`, `**/cache/**`), and case-insensitive separator normalization.
  - `NavigationLinkSkipTests`: nested directory junctions/symlinks are skipped and produce `Skipped` outcomes without traversing into outside targets.
- Solution build: 0 warnings, 0 errors.
- Standalone runtime smoke: executed real `SyncEngine` with real Windows junction (`mklink /J`), nested files, empty directory, `cache_to_prune/**` early directory pruning, `*.tmp` exclusion, and destination extra file preservation. Stdout: "PASS: Ticket 05 comprehensive runtime smoke test verified synchronously.", exit code 0.
- WPF Settings UI implemented: Item Type ComboBox (`Auto`, `File`, `Directory`), multi-line Exclusions editor, and Folder/File-aware Browse dialogs (using `OpenFolderDialog` for Directory and choice prompt for Auto).
- Flow UI acceptance: user confirmed live Flow Launcher verification. Folder synchronization works, Item Type and Exclusions persist, and the Browse button correctly opens folder selection dialogs for directory items.
- Host environment note: live OneDrive/Dropbox online-only hydration was not available in this local test environment; reparse handling adheres to ADR 0003 by distinguishing Navigation Links (`LinkTarget`) from ordinary files without blanket reparse-point rejection.
- Commits: no new commits created, maintaining compliance with AGENTS.md.

## Answer

Ticket 05 is complete. Directory synchronization recursively streams directory contents and replicates relative structure and empty directories to all destinations while leaving destination extras untouched. Exclusion patterns support glob-like matching with early subtree pruning before descent. Nested navigation links are safely skipped, and Auto item detection correctly categorizes files and directories at runtime. Settings UI includes Item Type selection, multi-line exclusions, and folder-aware browse dialogs. Per user explicit decision, criteria 18-19 are documented as satisfied under ADR 0003 architecture without live cloud-provider fixtures on this host environment. All 74 unit tests pass, compiler build has 0 warnings and 0 errors, and live UI acceptance was confirmed in Flow Launcher.
