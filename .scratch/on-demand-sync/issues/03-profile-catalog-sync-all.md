# 03: Profile catalog and Sync All

**What to build:** A user can manage a real catalog of Sync Profiles and Sync Items, search enabled or disabled profiles, run one profile, or run every enabled profile through `sy all` in stable sequential order.

**Blocked by:** 02: Portable and conflict-safe paths.

**Parent specification:** [SnapSync On-Demand Synchronization](../spec.md)

**Status:** ready-for-agent

- [ ] Settings support adding, renaming, editing, duplicating, deleting, enabling, disabling, and reordering Sync Profiles and Sync Items.
- [ ] Profile names are trimmed and unique case-insensitively across the configuration; item names are unique case-insensitively within a profile.
- [ ] `all` is reserved and profile names cannot begin with `:`; duplicate actions generate an unambiguous copy name.
- [ ] Every saved object remains internally valid; cross-profile path conflicts block save or enable only when all conflicting participants are enabled.
- [ ] `sy` lists enabled profiles and visibly disabled profiles with useful item-count subtitles.
- [ ] Selecting a disabled profile opens editing and cannot synchronize it as a one-time exception.
- [ ] `sy all` runs all enabled profiles, skips disabled profiles and items, and aggregates a deterministic summary.
- [ ] Profiles and items run sequentially in configured order; no parallel copy behavior is added.
- [ ] Delete operations require confirmation, and saved catalog changes survive Flow restart.
- [ ] The no-profile state offers a direct path to create the first profile.

## Testing seam

Test catalog validation and `Sync All` through public core operations with multiple temporary Sources. Assert unique-name rules, disabled omission, stable aggregate outcomes, structural preflight, and that unrelated enabled profiles synchronize in configured order.

## Demo path

Create two enabled profiles and one disabled profile, fuzzy-search each by abbreviation, run one profile, then run `sy all` and observe only the enabled profiles in the aggregate summary.
