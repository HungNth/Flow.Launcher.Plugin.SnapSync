using System.Collections.Generic;

namespace SnapSync.Core;

/// <summary>
/// Represents root configuration settings for SnapSync.
/// </summary>
public sealed class SnapSyncConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SnapSyncConfiguration"/> class.
    /// </summary>
    public SnapSyncConfiguration()
    {
    }

    /// <summary>
    /// Gets or sets the schema version of the configuration.
    /// </summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>
    /// Gets or sets the collection of configured synchronization profiles.
    /// </summary>
    public List<SyncProfile> Profiles { get; set; } = [];
}
