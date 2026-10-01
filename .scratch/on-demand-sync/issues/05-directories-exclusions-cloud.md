# 05: Recursive directories, exclusions, and cloud-safe traversal

**What to build:** A user can synchronize directory trees, including empty directories, while exclusions prune unwanted content and navigation links cannot escape the configured Source. Cloud placeholders remain eligible and hydrate only when explicit work needs their contents.

**Blocked by:** 03: Profile catalog and Sync All.

**Parent specification:** [SnapSync On-Demand Synchronization](../spec.md)

**Status:** ready-for-agent

- [ ] Sync Items support Auto, File, and Directory modes; Auto detects the current Source type for each operation.
- [ ] A missing Auto Source reports MissingSource, and an explicit File/Directory type mismatch reports a clear failure.
- [ ] Directory synchronization recursively preserves relative structure and creates missing Destination roots and empty directories.
- [ ] Additional files and directories already present only at a Destination remain untouched.
- [ ] Exclusion patterns are relative to the Source root, canonicalize separators to `/`, match case-insensitively, and support `*`, `?`, and `**`.
- [ ] Excluded directories are pruned before comparison and produce one Excluded outcome rather than outcomes for every descendant.
- [ ] Nested symbolic links, junctions, and mount points are not followed and produce Skipped outcomes.
- [ ] Non-navigation reparse points, including supported OneDrive and Dropbox placeholders, are treated as ordinary entries.
- [ ] Online-only content hydrates only when hashing or copying needs bytes; hydration failure is isolated and reported.
- [ ] Traversal streams work and results without constructing an unnecessary full in-memory directory tree.

## Testing seam

Use real temporary directory trees through the public core APIs. Cover nesting, empty directories, exclusions and pruning, extra Destination entries, Auto detection, explicit type mismatch, navigation-link skipping, and cloud-compatible reparse classification where the host environment permits deterministic setup.

## Demo path

Configure a directory containing nested files, an empty directory, an excluded cache subtree, and a navigation link. Synchronize it and verify the expected tree, untouched Destination extras, excluded subtree, and Skipped link outcome.
