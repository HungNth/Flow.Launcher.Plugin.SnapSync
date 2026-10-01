using System;
using System.Collections.Generic;

namespace SnapSync.Core;

/// <summary>
/// Represents a named, reusable collection of synchronization items.
/// </summary>
public sealed class SyncProfile
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SyncProfile"/> class.
    /// </summary>
    public SyncProfile()
    {
    }

    /// <summary>
    /// Gets or sets the unique identifier of the synchronization profile.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Gets or sets the unique name of the synchronization profile.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional user-facing description for the profile.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this synchronization profile is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the synchronization items belonging to this profile.
    /// </summary>
    public List<SyncItem> Items { get; set; } = [];
}
