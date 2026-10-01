using System;
using System.Collections.Generic;

namespace SnapSync.Core;

/// <summary>
/// Represents a configured synchronization item.
/// </summary>
public sealed class SyncItem
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SyncItem"/> class.
    /// </summary>
    public SyncItem()
    {
    }

    /// <summary>
    /// Gets or sets the unique identifier of the synchronization item.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Gets or sets the friendly name of the synchronization item.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the authoritative source path.
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of exact destination paths.
    /// </summary>
    public List<string> Destinations { get; set; } = [];

    /// <summary>
    /// Gets or sets the type of synchronization item.
    /// </summary>
    public SyncItemType ItemType { get; set; } = SyncItemType.File;

    /// <summary>
    /// Gets or sets the comparison mode used to detect changes.
    /// </summary>
    public ComparisonMode ComparisonMode { get; set; } = ComparisonMode.Fast;

    /// <summary>
    /// Gets or sets the relative exclusion patterns.
    /// </summary>
    public List<string> Exclusions { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether this synchronization item is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
