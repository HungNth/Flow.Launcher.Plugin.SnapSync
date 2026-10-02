using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace SnapSync.Core.Tests;

public sealed class ProfileCatalogTests : IDisposable
{
    private readonly string _testRoot;
    private readonly SyncEngine _engine;

    public ProfileCatalogTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "SnapSync-catalog-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);
        _engine = new SyncEngine();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }

    [Theory]
    [InlineData("all")]
    [InlineData("ALL")]
    [InlineData("All")]
    public void ValidateProfile_ReservedNameAll_ReturnsValidationError(string reservedName)
    {
        var profile = new SyncProfile
        {
            Name = reservedName,
            Items =
            [
                new SyncItem
                {
                    Name = "Item1",
                    Source = Path.Combine(_testRoot, "src.txt"),
                    Destinations = [Path.Combine(_testRoot, "dst.txt")]
                }
            ]
        };

        var errors = _engine.Validate(profile);

        Assert.Contains(errors, e => e.PropertyName == nameof(SyncProfile.Name) && e.Message.Contains("reserved", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(":report")]
    [InlineData(":internal")]
    public void ValidateProfile_NameStartingWithColon_ReturnsValidationError(string colonName)
    {
        var profile = new SyncProfile
        {
            Name = colonName,
            Items =
            [
                new SyncItem
                {
                    Name = "Item1",
                    Source = Path.Combine(_testRoot, "src.txt"),
                    Destinations = [Path.Combine(_testRoot, "dst.txt")]
                }
            ]
        };

        var errors = _engine.Validate(profile);

        Assert.Contains(errors, e => e.PropertyName == nameof(SyncProfile.Name) && e.Message.Contains("':'", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateConfiguration_DuplicateProfileNamesCaseInsensitive_ReturnsValidationError()
    {
        var config = new SnapSyncConfiguration
        {
            Profiles =
            [
                new SyncProfile
                {
                    Name = "BackupWork",
                    Items = [new SyncItem { Name = "I1", Source = Path.Combine(_testRoot, "s1.txt"), Destinations = [Path.Combine(_testRoot, "d1.txt")] }]
                },
                new SyncProfile
                {
                    Name = "  backupwork  ",
                    Items = [new SyncItem { Name = "I2", Source = Path.Combine(_testRoot, "s2.txt"), Destinations = [Path.Combine(_testRoot, "d2.txt")] }]
                }
            ]
        };

        var errors = _engine.Validate(config);

        Assert.Contains(errors, e => e.PropertyName == nameof(SyncProfile.Name) && e.Message.Contains("unique", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateProfile_DuplicateItemNamesWithinProfile_ReturnsValidationError()
    {
        var profile = new SyncProfile
        {
            Name = "Work",
            Items =
            [
                new SyncItem { Name = "Documents", Source = Path.Combine(_testRoot, "s1.txt"), Destinations = [Path.Combine(_testRoot, "d1.txt")] },
                new SyncItem { Name = "  documents  ", Source = Path.Combine(_testRoot, "s2.txt"), Destinations = [Path.Combine(_testRoot, "d2.txt")] }
            ]
        };

        var errors = _engine.Validate(profile);

        Assert.Contains(errors, e => e.PropertyName == nameof(SyncItem.Name) && e.Message.Contains("unique", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SynchronizeAllAsync_SequentiallySyncsEnabledProfilesInConfiguredOrder()
    {
        var src1 = Path.Combine(_testRoot, "src1.txt");
        var dst1 = Path.Combine(_testRoot, "dst1.txt");
        await File.WriteAllTextAsync(src1, "content1");

        var src2 = Path.Combine(_testRoot, "src2.txt");
        var dst2 = Path.Combine(_testRoot, "dst2.txt");
        await File.WriteAllTextAsync(src2, "content2");

        var srcDisabled = Path.Combine(_testRoot, "srcDisabled.txt");
        var dstDisabled = Path.Combine(_testRoot, "dstDisabled.txt");
        await File.WriteAllTextAsync(srcDisabled, "disabled");

        var config = new SnapSyncConfiguration
        {
            Profiles =
            [
                new SyncProfile
                {
                    Name = "First",
                    Enabled = true,
                    Items = [new SyncItem { Name = "Item1", Source = src1, Destinations = [dst1] }]
                },
                new SyncProfile
                {
                    Name = "DisabledProfile",
                    Enabled = false,
                    Items = [new SyncItem { Name = "ItemDisabled", Source = srcDisabled, Destinations = [dstDisabled] }]
                },
                new SyncProfile
                {
                    Name = "Second",
                    Enabled = true,
                    Items = [new SyncItem { Name = "Item2", Source = src2, Destinations = [dst2] }]
                }
            ]
        };

        var report = await _engine.SynchronizeAllAsync(config);

        Assert.Equal(2, report.CopiedCount);
        Assert.Equal(0, report.FailedCount);
        Assert.Equal(0, report.UnchangedCount);
        Assert.Equal("content1", await File.ReadAllTextAsync(dst1));
        Assert.Equal("content2", await File.ReadAllTextAsync(dst2));
        Assert.False(File.Exists(dstDisabled));
    }

    [Fact]
    public async Task SynchronizeAllAsync_WhenCatalogHasStructuralConflict_AbortsBeforeAnyWrites()
    {
        var src1 = Path.Combine(_testRoot, "valid_src.txt");
        var dst1 = Path.Combine(_testRoot, "valid_dst.txt");
        await File.WriteAllTextAsync(src1, "valid_content");

        var sharedTarget = Path.Combine(_testRoot, "target.txt");
        var config = new SnapSyncConfiguration
        {
            Profiles =
            [
                new SyncProfile
                {
                    Name = "ProfileA",
                    Enabled = true,
                    Items = [new SyncItem { Name = "ItemA", Source = src1, Destinations = [dst1] }]
                },
                new SyncProfile
                {
                    Name = "ProfileB",
                    Enabled = true,
                    Items =
                    [
                        new SyncItem { Name = "ConflictWrite1", Source = Path.Combine(_testRoot, "sA.txt"), Destinations = [sharedTarget] },
                        new SyncItem { Name = "ConflictWrite2", Source = Path.Combine(_testRoot, "sB.txt"), Destinations = [sharedTarget] }
                    ]
                }
            ]
        };

        var report = await _engine.SynchronizeAllAsync(config);

        Assert.False(report.IsSuccess);
        Assert.Equal(0, report.CopiedCount);
        Assert.False(File.Exists(dst1), "ProfileA must not perform writes because catalog preflight failed.");
    }
}
