namespace SnapSync.Core;

/// <summary>
/// Specifies the outcome of a synchronization operation for an entry.
/// </summary>
public enum EntryOutcome
{
    /// <summary>
    /// The entry was successfully copied or replaced.
    /// </summary>
    Copied,

    /// <summary>
    /// The destination entry was determined to be unchanged and was skipped.
    /// </summary>
    Unchanged,

    /// <summary>
    /// The entry failed to synchronize.
    /// </summary>
    Failed
}
