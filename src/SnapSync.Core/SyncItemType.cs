namespace SnapSync.Core;

/// <summary>
/// Specifies the type of synchronization item.
/// </summary>
public enum SyncItemType
{
    /// <summary>
    /// Synchronizes a single file to an exact destination file path.
    /// </summary>
    File = 0,

    /// <summary>
    /// Synchronizes a directory tree to an exact destination root directory.
    /// </summary>
    Directory = 1,

    /// <summary>
    /// Automatically detects whether the source is a file or a directory.
    /// </summary>
    Auto = 2
}
