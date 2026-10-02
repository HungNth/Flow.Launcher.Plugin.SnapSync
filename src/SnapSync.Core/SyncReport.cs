using System.Collections.Generic;

namespace SnapSync.Core;

/// <summary>
/// Represents the aggregate outcome of a synchronization or preview operation.
/// </summary>
/// <param name="CopiedCount">The number of files copied, replaced, or (in preview) that would be copied.</param>
/// <param name="UnchangedCount">The number of files unchanged.</param>
/// <param name="FailedCount">The number of entries that failed, were missing source, or could not be processed.</param>
/// <param name="Entries">The detailed per-entry results.</param>
/// <param name="IsTruncated">Indicates whether the cached entries list was truncated to cap memory usage.</param>
/// <param name="TotalDiscoveredCount">The total count of all evaluated entries across the entire operation.</param>
/// <param name="IsCancelled">Indicates whether the operation was cancelled before completing all items.</param>
public sealed record SyncReport(
    int CopiedCount,
    int UnchangedCount,
    int FailedCount,
    IReadOnlyList<EntryResult> Entries,
    bool IsTruncated = false,
    int TotalDiscoveredCount = 0,
    bool IsCancelled = false)
{
    /// <summary>
    /// Gets a value indicating whether all processed entries succeeded without failures.
    /// </summary>
    public bool IsSuccess => FailedCount == 0 && !IsCancelled;
}
