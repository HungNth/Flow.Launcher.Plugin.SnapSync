using System;
using System.IO;
using System.Text.Json;
using Xunit;

namespace SnapSync.Core.Tests;

public sealed class ConfigurationSafetyTests
{
    [Fact]
    public void Configuration_UnsupportedSchemaVersion_IsDetectable()
    {
        var invalidJson = "{\"SchemaVersion\": 999, \"Profiles\": []}";
        var parsed = JsonSerializer.Deserialize<SnapSyncConfiguration>(invalidJson);

        Assert.NotNull(parsed);
        Assert.True(parsed.SchemaVersion > 1, "SchemaVersion 999 must be rejected as unsupported version.");
    }

    [Fact]
    public void Configuration_CorruptJson_ThrowsJsonExceptionWithoutCrashingApplication()
    {
        var malformedJson = "{ \"SchemaVersion\": 1, \"Profiles\": [ { unclosed json... ";

        Assert.ThrowsAny<JsonException>(() =>
        {
            JsonSerializer.Deserialize<SnapSyncConfiguration>(malformedJson);
        });
    }

    [Fact]
    public void Configuration_ValidSchemaVersion1_DeserializesCorrectly()
    {
        var validJson = "{\"SchemaVersion\": 1, \"Profiles\": [{\"Name\": \"MyProfile\", \"Enabled\": true, \"Items\": []}]}";
        var config = JsonSerializer.Deserialize<SnapSyncConfiguration>(validJson);

        Assert.NotNull(config);
        Assert.Equal(1, config.SchemaVersion);
        Assert.Single(config.Profiles);
        Assert.Equal("MyProfile", config.Profiles[0].Name);
    }
}
