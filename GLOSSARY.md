# SnapSync

SnapSync is an on-demand, one-way synchronization context for explicitly pushing selected local files and directories from authoritative sources to exact destination paths.

## Language

**Sync Profile**:
A named, reusable collection of Sync Items that can be previewed or synchronized together.
_Avoid_: Job, plan, group

**Sync Item**:
A configured synchronization unit containing one Source and one or more Destinations.
_Avoid_: Mapping, rule, task

**Source**:
The authoritative file or directory whose state is pushed outward during synchronization.
_Avoid_: Origin, input

**Destination**:
An exact final target file path or directory root that receives content from a Source. SnapSync never appends the Source name implicitly.
_Avoid_: Backup folder, target container

**Synchronization**:
An explicitly triggered, one-way push that copies new or changed Source content and preserves its directory structure, including empty directories, without deleting unrelated destination content.
_Avoid_: Mirroring, replication, two-way sync

**Preview**:
A read-only evaluation of the changes a synchronization would make.
_Avoid_: Simulation, scan

**Synchronization Report**:
The structured outcome of a Preview or synchronization, including per-entry results and aggregate counts.
_Avoid_: Log, notification

**Path Conflict**:
An invalid configuration in which configured read and write areas overlap in a way that can recurse or make the final state depend on execution order.
_Avoid_: Destination conflict, target collision, sync chain

**Unchanged File**:
A Source file whose corresponding Destination file is equivalent under the Sync Item's selected comparison mode.
_Avoid_: Skipped file

**Excluded Entry**:
A Source entry intentionally omitted because it matches an exclusion pattern. Excluding a directory excludes its entire subtree.
_Avoid_: Skipped entry

**Navigation Link**:
A filesystem entry that redirects path traversal to another named location, such as a symbolic link, junction, or mount point. Cloud-backed placeholder files are not Navigation Links.
_Avoid_: Reparse point, linked entry

**Skipped Entry**:
A Source entry intentionally left unprocessed by a filesystem safety rule, such as the rule against following Navigation Links.
_Avoid_: Excluded entry
