namespace SnapSync.Core;

/// <summary>
/// Specifies the outcome of a preview or synchronization operation for an entry.
/// </summary>
public enum EntryOutcome
{
    /// <summary>
    /// The entry was successfully copied or replaced.
    /// </summary>
    Copied,

    /// <summary>
    /// Preview outcome: the entry would be copied or replaced.
    /// </summary>
    WouldCopy,

    /// <summary>
    /// The destination entry was determined to be unchanged and was skipped.
    /// </summary>
    Unchanged,

    /// <summary>
    /// The directory was newly created.
    /// </summary>
    CreatedDirectory,

    /// <summary>
    /// Preview outcome: the directory would be newly created.
    /// </summary>
    WouldCreateDirectory,

    /// <summary>
    /// The source path does not exist.
    /// </summary>
    MissingSource,

    /// <summary>
    /// The entry was excluded by configured pattern rules.
    /// </summary>
    Excluded,

    /// <summary>
    /// The entry was skipped (such as an internal navigation link).
    /// </summary>
    Skipped,

    /// <summary>
    /// The entry failed to synchronize.
    /// </summary>
    Failed
}
