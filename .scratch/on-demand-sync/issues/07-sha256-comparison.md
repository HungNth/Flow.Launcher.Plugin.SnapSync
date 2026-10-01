# 07: SHA-256 comparison across files and directories

**What to build:** A user can select SHA-256 comparison per Sync Item for files or directory trees, including Preview and multiple Destinations, without unnecessary metadata writes or repeated Source hashing.

**Blocked by:** 06: Preview and cached Flow reports.

**Parent specification:** [SnapSync On-Demand Synchronization](../spec.md)

**Status:** ready-for-agent

- [ ] The item editor offers Fast and SHA-256 comparison modes with Fast as the default.
- [ ] SHA-256 hashing streams file contents and honors operation cancellation.
- [ ] A Source hash is reused across Destinations for the same Source file within an operation instead of being recomputed unnecessarily.
- [ ] Missing Destinations do not trigger unnecessary Destination hashing.
- [ ] Equal content produces Unchanged even when timestamps differ and does not rewrite Destination metadata.
- [ ] Different content produces WouldCopy in Preview and Copied after successful synchronization.
- [ ] Hash failures are categorized per affected Source or Destination and do not abort unrelated entries.
- [ ] Hash Preview may hydrate online-only cloud content because it explicitly requires file bytes.
- [ ] Query handling remains filesystem-free and never hashes while the user types.
- [ ] Memory use remains bounded regardless of file size.

## Testing seam

Use data-driven real-file tests through public Preview and synchronization operations. Cover same content with different timestamps, same size with different content, multiple Destinations with Source-hash reuse observable through progress/diagnostics rather than mock forwarding, cancellation, and hydration failure where deterministic.

## Demo path

Create equal-length files with different content, select SHA-256, Preview the required copy, synchronize, alter only a Destination timestamp, and observe Unchanged with no metadata mutation.
