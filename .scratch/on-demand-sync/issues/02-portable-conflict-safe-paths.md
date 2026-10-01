# 02: Portable and conflict-safe paths

**What to build:** A user can configure portable Source and Destination paths while SnapSync blocks ambiguous, recursive, aliased, or order-dependent path relationships before any write. Runtime path behavior is the same in settings validation, Preview, and synchronization.

**Blocked by:** 01: One-file Fast synchronization end to end.

**Parent specification:** [SnapSync On-Demand Synchronization](../spec.md)

**Status:** ready-for-agent

- [ ] A leading `~` and Windows environment variables expand at operation time for Sources and Destinations.
- [ ] Expanded paths must be rooted drive or UNC paths; unresolved variables and relative paths are validation errors.
- [ ] File Destinations are exact target files and directory Destinations are exact target roots; SnapSync never appends a Source basename implicitly.
- [ ] Normalization is case-insensitive on Windows and detects duplicate Destinations and identical Source/Destination paths.
- [ ] Both Source-inside-Destination and Destination-inside-Source relationships are rejected.
- [ ] Enabled write/write and read/write overlaps are detected across the configuration so execution order cannot determine final output.
- [ ] Existing path ancestors that are symbolic links, junctions, or mount points are rejected, while non-navigation cloud placeholders remain valid.
- [ ] Structural validation covers the entire selected operation and aborts before all writes when any structural error exists.
- [ ] Missing Sources and temporarily unavailable network/removable paths remain saveable warnings and become runtime outcomes rather than structural save failures.
- [ ] Settings show actionable inline messages for every rejected path relationship.

## Testing seam

Use data-driven tests through the public core validation and operation APIs. Cover environment expansion, home expansion, unresolved variables, UNC paths, case variants, containment in both directions, cross-item overlaps, and navigation-link ancestors with real temporary filesystem objects where supported.

## Demo path

Configure a portable `%USERPROFILE%` Source and a valid Destination, then demonstrate save-time rejection for same-path, recursive, and junction-aliased variants without any Destination mutation.
