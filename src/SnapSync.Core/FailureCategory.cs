namespace SnapSync.Core;

/// <summary>
/// Specifies the category of a failure encountered during synchronization or preflight.
/// </summary>
public enum FailureCategory
{
    /// <summary>
    /// No failure occurred for this entry.
    /// </summary>
    None = 0,

    /// <summary>
    /// The source file or directory does not exist or is offline.
    /// </summary>
    MissingSource = 1,

    /// <summary>
    /// The destination path, drive, or network share is unavailable or invalid.
    /// </summary>
    UnavailableDestination = 2,

    /// <summary>
    /// Access was denied or permission was insufficient.
    /// </summary>
    PermissionDenied = 3,

    /// <summary>
    /// The file is locked or in use by another process.
    /// </summary>
    LockedFile = 4,

    /// <summary>
    /// There is insufficient disk space on the destination drive.
    /// </summary>
    DiskFull = 5,

    /// <summary>
    /// The destination is read-only.
    /// </summary>
    ReadOnlyDestination = 6,

    /// <summary>
    /// The path is malformed or exceeds system limits.
    /// </summary>
    InvalidPath = 7,

    /// <summary>
    /// Content hash computation failed or differed unexpectedly.
    /// </summary>
    HashFailure = 8,

    /// <summary>
    /// Cloud placeholder hydration failed.
    /// </summary>
    HydrationFailure = 9,

    /// <summary>
    /// The source file was mutated during transfer.
    /// </summary>
    SourceMutated = 10,

    /// <summary>
    /// Structural preflight or configuration error.
    /// </summary>
    ValidationError = 11,

    /// <summary>
    /// Generic or unexpected I/O failure.
    /// </summary>
    IoError = 12
}
