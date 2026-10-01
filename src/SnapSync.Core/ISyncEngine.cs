using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SnapSync.Core;

/// <summary>
/// Defines the high-level synchronization and validation operations.
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
    /// Synchronizes the enabled items of the specified profile.
    /// </summary>
    /// <param name="profile">The synchronization profile to execute.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>A task representing the asynchronous operation, yielding the synchronization report.</returns>
    Task<SyncReport> SynchronizeAsync(SyncProfile profile, CancellationToken cancellationToken = default);
}
