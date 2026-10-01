namespace SnapSync.Core;

/// <summary>
/// Represents the result of synchronizing a single entry.
/// </summary>
/// <param name="Source">The authoritative source path.</param>
/// <param name="Destination">The target destination path, if applicable.</param>
/// <param name="Outcome">The synchronization outcome.</param>
/// <param name="ErrorMessage">An optional error message describing a failure.</param>
public sealed record EntryResult(
    string Source,
    string? Destination,
    EntryOutcome Outcome,
    string? ErrorMessage = null);
