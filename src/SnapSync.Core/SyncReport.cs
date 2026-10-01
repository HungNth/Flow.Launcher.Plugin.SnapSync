using System.Collections.Generic;

namespace SnapSync.Core;

/// <summary>
/// Represents the aggregate outcome of a synchronization operation.
/// </summary>
/// <param name="CopiedCount">The number of files copied or replaced.</param>
/// <param name="UnchangedCount">The number of files unchanged.</param>
/// <param name="FailedCount">The number of files that failed to synchronize.</param>
/// <param name="Entries">The detailed per-entry results.</param>
public sealed record SyncReport(
    int CopiedCount,
    int UnchangedCount,
    int FailedCount,
    IReadOnlyList<EntryResult> Entries)
{
    /// <summary>
    /// Gets a value indicating whether all processed entries succeeded without failures.
    /// </summary>
    public bool IsSuccess => FailedCount == 0;
}
