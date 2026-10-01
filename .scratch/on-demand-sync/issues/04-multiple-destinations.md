# 04: Multiple Destinations with independent outcomes

**What to build:** A user can attach several exact Destinations to one Sync Item and receive an independent outcome for every Source–Destination transfer, including mixed success when one location is unavailable.

**Blocked by:** 03: Profile catalog and Sync All.

**Parent specification:** [SnapSync On-Demand Synchronization](../spec.md)

**Status:** ready-for-agent

- [ ] The item editor supports adding, editing, removing, inspecting, and usefully reordering multiple Destinations.
- [ ] Duplicate normalized Destinations and overlapping Destination write areas are rejected before save or execution.
- [ ] Each Destination is compared and processed independently in stable configured order.
- [ ] Failure at one Destination does not prevent remaining independent Destinations from being processed.
- [ ] Copied, unchanged, and failed counts are per Source–Destination outcome.
- [ ] Missing Source and excluded outcomes remain Source-level and are not multiplied by Destination count.
- [ ] Summary and detail results identify the affected Destination when applicable.
- [ ] Fail-safe temporary replacement is applied independently in each Destination directory.
- [ ] A Destination that is offline, locked, read-only, or otherwise unavailable remains configured after failure.
- [ ] Settings and Flow subtitles make Destination counts easy to inspect.

## Testing seam

Run the public core synchronization API against two or more real temporary Destinations. Cover all-success, one-fails, all-fail, mixed unchanged/copied states, duplicate validation, and exact per-transfer counters.

## Demo path

Configure one Source file with two Destinations, synchronize both, make one Destination unavailable, edit the Source, and observe one successful transfer plus one isolated failure.
