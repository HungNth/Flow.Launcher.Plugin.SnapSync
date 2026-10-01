using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace SnapSync.Core.Tests;

/// <summary>
/// Unit tests for <see cref="SyncEngine"/> exercising the public core synchronization and validation seam.
/// </summary>
public sealed class SyncEngineTests : IDisposable
{
    private readonly string _testRoot;
    private readonly ISyncEngine _engine;

    public SyncEngineTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "SnapSyncTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);
        _engine = new SyncEngine();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testRoot))
            {
                Directory.Delete(_testRoot, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup in test temp directory.
        }
    }

    [Fact]
    public void Validate_WhenProfileNameIsEmpty_ReturnsValidationError()
    {
        var profile = new SyncProfile
        {
            Name = "  ",
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

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.PropertyName == nameof(SyncProfile.Name));
    }

    [Fact]
    public void Validate_WhenProfileHasNoItems_ReturnsValidationError()
    {
        var profile = new SyncProfile
        {
            Name = "ValidProfile",
            Items = []
        };

        var errors = _engine.Validate(profile);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.PropertyName == nameof(SyncProfile.Items));
    }

    [Fact]
    public void Validate_WhenItemSourceOrDestinationsEmpty_ReturnsValidationError()
    {
        var profile = new SyncProfile
        {
            Name = "ValidProfile",
            Items =
            [
                new SyncItem
                {
                    Name = "Item1",
                    Source = "",
                    Destinations = []
                }
            ]
        };

        var errors = _engine.Validate(profile);

        Assert.Contains(errors, e => e.PropertyName == nameof(SyncItem.Source));
        Assert.Contains(errors, e => e.PropertyName == nameof(SyncItem.Destinations));
    }

    [Fact]
    public async Task SynchronizeAsync_WhenDestinationMissing_CopiesFileAndPreservesTimestamp()
    {
        var sourcePath = Path.Combine(_testRoot, "source.txt");
        var destPath = Path.Combine(_testRoot, "dest", "target.txt");
        var content = "Authoritative configuration content v1";
        await File.WriteAllTextAsync(sourcePath, content);
        var expectedTime = new DateTime(2025, 6, 15, 12, 30, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(sourcePath, expectedTime);

        var profile = new SyncProfile
        {
            Name = "TestProfile",
            Items =
            [
                new SyncItem
                {
                    Name = "ConfigFile",
                    Source = sourcePath,
                    Destinations = [destPath],
                    ItemType = SyncItemType.File,
                    ComparisonMode = ComparisonMode.Fast
                }
            ]
        };

        var report = await _engine.SynchronizeAsync(profile);

        Assert.True(report.IsSuccess);
        Assert.Equal(1, report.CopiedCount);
        Assert.Equal(0, report.UnchangedCount);
        Assert.Equal(0, report.FailedCount);
        Assert.Single(report.Entries);
        Assert.Equal(EntryOutcome.Copied, report.Entries[0].Outcome);

        Assert.True(File.Exists(destPath));
        var destContent = await File.ReadAllTextAsync(destPath);
        Assert.Equal(content, destContent);
        Assert.Equal(expectedTime, File.GetLastWriteTimeUtc(destPath));
    }

    [Fact]
    public async Task SynchronizeAsync_WhenDestinationUnchangedWithinTwoSeconds_SkipsCopy()
    {
        var sourcePath = Path.Combine(_testRoot, "source.txt");
        var destPath = Path.Combine(_testRoot, "target.txt");
        var content = "Same content";
        await File.WriteAllTextAsync(sourcePath, content);
        await File.WriteAllTextAsync(destPath, content);

        var baseTime = new DateTime(2025, 6, 15, 12, 30, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(sourcePath, baseTime);
        // Destination timestamp is 1.5 seconds off (within 2-second tolerance)
        File.SetLastWriteTimeUtc(destPath, baseTime.AddSeconds(1.5));

        var profile = new SyncProfile
        {
            Name = "TestProfile",
            Items =
            [
                new SyncItem
                {
                    Name = "ConfigFile",
                    Source = sourcePath,
                    Destinations = [destPath],
                    ItemType = SyncItemType.File,
                    ComparisonMode = ComparisonMode.Fast
                }
            ]
        };

        var report = await _engine.SynchronizeAsync(profile);

        Assert.True(report.IsSuccess);
        Assert.Equal(0, report.CopiedCount);
        Assert.Equal(1, report.UnchangedCount);
        Assert.Equal(0, report.FailedCount);
        Assert.Equal(EntryOutcome.Unchanged, report.Entries[0].Outcome);
        // Timestamp on destination was not overwritten
        Assert.Equal(baseTime.AddSeconds(1.5), File.GetLastWriteTimeUtc(destPath));
    }

    [Fact]
    public async Task SynchronizeAsync_WhenDestinationTimestampDiffersByMoreThanTwoSeconds_ReplacesFile()
    {
        var sourcePath = Path.Combine(_testRoot, "source.txt");
        var destPath = Path.Combine(_testRoot, "target.txt");
        var content = "Content with differing timestamps";
        await File.WriteAllTextAsync(sourcePath, content);
        await File.WriteAllTextAsync(destPath, content);

        var baseTime = new DateTime(2025, 6, 15, 12, 30, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(sourcePath, baseTime);
        // Destination timestamp differs by 3 seconds (> 2-second tolerance)
        File.SetLastWriteTimeUtc(destPath, baseTime.AddSeconds(3));

        var profile = new SyncProfile
        {
            Name = "TestProfile",
            Items =
            [
                new SyncItem
                {
                    Name = "ConfigFile",
                    Source = sourcePath,
                    Destinations = [destPath],
                    ItemType = SyncItemType.File,
                    ComparisonMode = ComparisonMode.Fast
                }
            ]
        };

        var report = await _engine.SynchronizeAsync(profile);

        Assert.True(report.IsSuccess);
        Assert.Equal(1, report.CopiedCount);
        Assert.Equal(0, report.UnchangedCount);
        Assert.Equal(0, report.FailedCount);
        Assert.Equal(EntryOutcome.Copied, report.Entries[0].Outcome);
        Assert.Equal(baseTime, File.GetLastWriteTimeUtc(destPath));
    }

    [Fact]
    public async Task SynchronizeAsync_WhenDestinationContentDiffers_ReplacesDestinationEvenIfNewer()
    {
        var sourcePath = Path.Combine(_testRoot, "source.txt");
        var destPath = Path.Combine(_testRoot, "target.txt");
        await File.WriteAllTextAsync(sourcePath, "Authoritative Source Content");
        await File.WriteAllTextAsync(destPath, "Old or modified destination");

        var sourceTime = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        var newerDestTime = new DateTime(2025, 6, 15, 14, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(sourcePath, sourceTime);
        File.SetLastWriteTimeUtc(destPath, newerDestTime);

        var profile = new SyncProfile
        {
            Name = "TestProfile",
            Items =
            [
                new SyncItem
                {
                    Name = "ConfigFile",
                    Source = sourcePath,
                    Destinations = [destPath],
                    ItemType = SyncItemType.File,
                    ComparisonMode = ComparisonMode.Fast
                }
            ]
        };

        var report = await _engine.SynchronizeAsync(profile);

        Assert.True(report.IsSuccess);
        Assert.Equal(1, report.CopiedCount);
        Assert.Equal(0, report.UnchangedCount);
        Assert.Equal("Authoritative Source Content", await File.ReadAllTextAsync(destPath));
        Assert.Equal(sourceTime, File.GetLastWriteTimeUtc(destPath));
    }

    [Fact]
    public async Task SynchronizeAsync_DoesNotDeleteExtraFilesInDestinationDirectory()
    {
        var destDir = Path.Combine(_testRoot, "dest");
        Directory.CreateDirectory(destDir);
        var extraFile = Path.Combine(destDir, "unrelated_file.txt");
        await File.WriteAllTextAsync(extraFile, "I should never be deleted");

        var sourcePath = Path.Combine(_testRoot, "source.txt");
        var destPath = Path.Combine(destDir, "target.txt");
        await File.WriteAllTextAsync(sourcePath, "New file");

        var profile = new SyncProfile
        {
            Name = "TestProfile",
            Items =
            [
                new SyncItem
                {
                    Name = "ConfigFile",
                    Source = sourcePath,
                    Destinations = [destPath],
                    ItemType = SyncItemType.File,
                    ComparisonMode = ComparisonMode.Fast
                }
            ]
        };

        var report = await _engine.SynchronizeAsync(profile);

        Assert.True(report.IsSuccess);
        Assert.True(File.Exists(extraFile));
        Assert.Equal("I should never be deleted", await File.ReadAllTextAsync(extraFile));
    }

    [Fact]
    public async Task SynchronizeAsync_WhenSourceDoesNotExist_ReportsFailureAndLeavesDestinationIntact()
    {
        var sourcePath = Path.Combine(_testRoot, "missing_source.txt");
        var destPath = Path.Combine(_testRoot, "target.txt");
        await File.WriteAllTextAsync(destPath, "Original intact destination");

        var profile = new SyncProfile
        {
            Name = "TestProfile",
            Items =
            [
                new SyncItem
                {
                    Name = "ConfigFile",
                    Source = sourcePath,
                    Destinations = [destPath],
                    ItemType = SyncItemType.File,
                    ComparisonMode = ComparisonMode.Fast
                }
            ]
        };

        var report = await _engine.SynchronizeAsync(profile);

        Assert.False(report.IsSuccess);
        Assert.Equal(0, report.CopiedCount);
        Assert.Equal(0, report.UnchangedCount);
        Assert.Equal(1, report.FailedCount);
        Assert.Equal(EntryOutcome.Failed, report.Entries[0].Outcome);
        Assert.NotNull(report.Entries[0].ErrorMessage);
        Assert.Equal("Original intact destination", await File.ReadAllTextAsync(destPath));
    }

    [Fact]
    public async Task SynchronizeAsync_WhenDisabledItem_IsSkipped()
    {
        var sourcePath = Path.Combine(_testRoot, "source.txt");
        var destPath = Path.Combine(_testRoot, "target.txt");
        await File.WriteAllTextAsync(sourcePath, "Disabled content");

        var profile = new SyncProfile
        {
            Name = "TestProfile",
            Items =
            [
                new SyncItem
                {
                    Name = "DisabledConfigFile",
                    Source = sourcePath,
                    Destinations = [destPath],
                    ItemType = SyncItemType.File,
                    ComparisonMode = ComparisonMode.Fast,
                    Enabled = false
                }
            ]
        };

        var report = await _engine.SynchronizeAsync(profile);

        Assert.True(report.IsSuccess);
        Assert.Equal(0, report.CopiedCount);
        Assert.Equal(0, report.UnchangedCount);
        Assert.Equal(0, report.FailedCount);
        Assert.Empty(report.Entries);
        Assert.False(File.Exists(destPath));
    }

    [Fact]
    public async Task SynchronizeAsync_CleansUpTemporaryStagingFileOnCompletion()
    {
        var sourcePath = Path.Combine(_testRoot, "source.txt");
        var destDir = Path.Combine(_testRoot, "target_dir");
        var destPath = Path.Combine(destDir, "final.txt");
        await File.WriteAllTextAsync(sourcePath, "Staging test content");

        var profile = new SyncProfile
        {
            Name = "TestProfile",
            Items =
            [
                new SyncItem
                {
                    Name = "StagedItem",
                    Source = sourcePath,
                    Destinations = [destPath],
                    ItemType = SyncItemType.File,
                    ComparisonMode = ComparisonMode.Fast
                }
            ]
        };

        var report = await _engine.SynchronizeAsync(profile);

        Assert.True(report.IsSuccess);
        Assert.True(File.Exists(destPath));

        // Check that no temporary .tmp files exist in destination directory
        var remainingFiles = Directory.GetFiles(destDir, "*.tmp.*");
        Assert.Empty(remainingFiles);
    }
}
