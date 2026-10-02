using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SnapSync.Core;

/// <summary>
/// Defines the high-level synchronization, preview, and validation operations.
/// </summary>
public interface ISyncEngine
{
    /// <summary>
    /// Validates the structure and settings of a synchronization profile.
    /// </summary>
    /// <param name="profile">The profile to validate.</param>
    /// <returns>A read-only list of validation errors, or empty if valid.</returns>
    IReadOnlyList<ValidationError> Validate(SyncProfile profile);

    /// <summary>
    /// Validates the whole-catalog configuration across all profiles and items.
    /// Enforces internal structural validity for all objects (including disabled objects)
    /// and cross-object conflict rules across enabled participants.
    /// </summary>
    /// <param name="configuration">The root configuration to validate.</param>
    /// <returns>A read-only list of validation errors, or empty if valid.</returns>
    IReadOnlyList<ValidationError> Validate(SnapSyncConfiguration configuration);

    /// <summary>
    /// Evaluates nonblocking runtime availability warnings for a synchronization profile,
    /// such as missing sources or temporarily unavailable/offline drives or network paths.
    /// </summary>
    /// <param name="profile">The profile to inspect.</param>
    /// <returns>A read-only list of warning messages, or empty if all paths are currently accessible.</returns>
    IReadOnlyList<string> GetAvailabilityWarnings(SyncProfile profile);

    /// <summary>
    /// Synchronizes all enabled profiles in the catalog sequentially in configured order.
    /// Preflights the entire catalog before performing any writes; if any structural error exists,
    /// aborts without mutating destinations.
    /// </summary>
    /// <param name="configuration">The configuration containing all profiles to synchronize.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>A task representing the asynchronous operation, yielding the aggregated synchronization report.</returns>
    Task<SyncReport> SynchronizeAllAsync(SnapSyncConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronizes the enabled items of the specified profile.
    /// Preflights the entire scope before performing any writes; if any structural error exists,
    /// aborts without mutating destinations.
    /// </summary>
    /// <param name="profile">The synchronization profile to execute.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>A task representing the asynchronous operation, yielding the synchronization report.</returns>
    Task<SyncReport> SynchronizeAsync(SyncProfile profile, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs a read-only preview of the specified profile without mutating files or directories.
    /// Follows the same discovery, comparison, and preflight rules as synchronization.
    /// </summary>
    /// <param name="profile">The synchronization profile to preview.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>A task representing the asynchronous operation, yielding the preview report.</returns>
    Task<SyncReport> PreviewAsync(SyncProfile profile, CancellationToken cancellationToken = default);
}
