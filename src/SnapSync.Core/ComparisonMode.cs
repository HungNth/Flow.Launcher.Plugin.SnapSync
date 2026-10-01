namespace SnapSync.Core;

/// <summary>
/// Specifies the comparison method used to determine whether a destination file is unchanged.
/// </summary>
public enum ComparisonMode
{
    /// <summary>
    /// Compares length and modification timestamp with a two-second tolerance.
    /// </summary>
    Fast = 0
}
