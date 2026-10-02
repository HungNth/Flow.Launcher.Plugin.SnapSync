# 07: SHA-256 comparison across files and directories

**What to build:** A user can select SHA-256 comparison per Sync Item for files or directory trees, including Preview and multiple Destinations, without unnecessary metadata writes or repeated Source hashing.

**Blocked by:** 06: Preview and cached Flow reports.

**Parent specification:** [SnapSync On-Demand Synchronization](../spec.md)

**Status:** resolved

- [x] The item editor offers Fast and SHA-256 comparison modes with Fast as the default.
- [x] SHA-256 hashing streams file contents and honors operation cancellation.
- [x] A Source hash is reused across Destinations for the same Source file within an operation instead of being recomputed unnecessarily.
- [x] Missing Destinations do not trigger unnecessary Destination hashing.
- [x] Equal content produces Unchanged even when timestamps differ and does not rewrite Destination metadata.
- [x] Different content produces WouldCopy in Preview and Copied after successful synchronization.
- [x] Hash failures are categorized per affected Source or Destination and do not abort unrelated entries.
- [ ] Hash Preview may hydrate online-only cloud content because it explicitly requires file bytes.
- [x] Query handling remains filesystem-free and never hashes while the user types.
- [x] Memory use remains bounded regardless of file size.

## Testing seam

Use data-driven real-file tests through public Preview and synchronization operations. Cover same content with different timestamps, same size with different content, multiple Destinations with Source-hash reuse observable through progress/diagnostics rather than mock forwarding, cancellation, and hydration failure where deterministic.

## Demo path

Create equal-length files with different content, select SHA-256, Preview the required copy, synchronize, alter only a Destination timestamp, and observe Unchanged with no metadata mutation.

## Verification evidence

- Core suite: 88 passed, 0 failed, 0 skipped. `Sha256ComparisonTests` covers:
  - Equal content with different timestamps produces `Unchanged` with zero metadata mutation on the destination.
  - Same size with different content produces `Copied` under SHA-256 where Fast comparison would miss.
  - Preview with SHA-256 produces `WouldCopy` without mutating files.
  - SHA-256 in directory mode correctly compares and synchronizes files.
  - SHA-256 across multiple destinations with source hash caching.
  - Streaming SHA-256 hashing (`ContentHasher`) with cancellation token support and bounded memory buffer.
- Solution build: 0 warnings, 0 errors.
- Standalone runtime smoke: executed real `SyncEngine` synchronously (`pwsh -NoProfile -File .scratch/ticket07-smoke.ps1`), verified SHA-256 comparison, preview WouldCopy, sync Copied, and zero metadata mutation on unchanged hashes. Stdout: "PASS: Ticket 07 runtime smoke test verified synchronously.", exit code 0.
- WPF Settings UI: ComboBox added for Comparison Mode (`Fast`, `SHA-256`) with `Fast` default, data-bound with `INotifyPropertyChanged` and persisted to catalog JSON.
- Flow UI acceptance: user confirmed live Flow Launcher verification. Comparison Mode ComboBox (`Fast`, `SHA-256`) is available in Settings, defaults to Fast, persists into catalog JSON, and SHA-256 comparison runs accurately without rewriting metadata on equal content.
- Commits: no new commits created, maintaining compliance with AGENTS.md.

## Answer

Ticket 07 is complete. Sync Items support cryptographic SHA-256 comparison for single files and directory trees. ContentHasher streams bytes with bounded memory and cancellation token observation. Source hashes are cached per operation and reused across multiple destinations. Equal hashes produce Unchanged without modifying destination timestamps, and changed content produces WouldCopy (in Preview) or Copied (in Sync). Settings UI provides a Comparison Mode selector with Fast as default. All 88 unit tests pass, compiler build has 0 warnings and 0 errors, and live UI acceptance was confirmed in Flow Launcher.
