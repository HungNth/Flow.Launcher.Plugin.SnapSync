using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace SnapSync.Core.Tests;

public sealed class MultipleDestinationsTests : IDisposable
{
    private readonly string _testRoot;
    private readonly SyncEngine _engine;

    public MultipleDestinationsTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "SnapSync-multidest-test-" + Guid.NewGuid().ToString("N"));
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

    [Fact]
    public async Task SynchronizeAsync_MultipleDestinations_AllSucceedIndependentOutcomes()
    {
        var src = Path.Combine(_testRoot, "source.txt");
        var dst1 = Path.Combine(_testRoot, "dst1.txt");
        var dst2 = Path.Combine(_testRoot, "dst2.txt");
        var dst3 = Path.Combine(_testRoot, "dst3.txt");
        await File.WriteAllTextAsync(src, "MultiDestinationPayload");

        var profile = new SyncProfile
        {
            Name = "MultiDst",
            Items =
            [
                new SyncItem
                {
                    Name = "Item1",
                    Source = src,
                    Destinations = [dst1, dst2, dst3]
                }
            ]
        };

        var report = await _engine.SynchronizeAsync(profile);

        Assert.Equal(3, report.CopiedCount);
        Assert.Equal(0, report.UnchangedCount);
        Assert.Equal(0, report.FailedCount);
        Assert.Equal("MultiDestinationPayload", await File.ReadAllTextAsync(dst1));
        Assert.Equal("MultiDestinationPayload", await File.ReadAllTextAsync(dst2));
        Assert.Equal("MultiDestinationPayload", await File.ReadAllTextAsync(dst3));
    }

    [Fact]
    public async Task SynchronizeAsync_OneDestinationLocked_RemainingDestinationsSucceedIndependently()
    {
        var src = Path.Combine(_testRoot, "source_isolated.txt");
        var dst1 = Path.Combine(_testRoot, "dst_valid1.txt");
        var dstLocked = Path.Combine(_testRoot, "dst_locked.txt");
        var dst2 = Path.Combine(_testRoot, "dst_valid2.txt");

        await File.WriteAllTextAsync(src, "NewVersionContent");
        await File.WriteAllTextAsync(dstLocked, "OriginalLockedContent");

        // Lock dstLocked exclusively
        using var lockStream = new FileStream(dstLocked, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var profile = new SyncProfile
        {
            Name = "PartialLockProfile",
            Items =
            [
                new SyncItem
                {
                    Name = "Item1",
                    Source = src,
                    Destinations = [dst1, dstLocked, dst2]
                }
            ]
        };

        var report = await _engine.SynchronizeAsync(profile);

        // 2 copied, 1 failed
        Assert.Equal(2, report.CopiedCount);
        Assert.Equal(1, report.FailedCount);
        Assert.Equal(0, report.UnchangedCount);

        Assert.Equal("NewVersionContent", await File.ReadAllTextAsync(dst1));
        Assert.Equal("NewVersionContent", await File.ReadAllTextAsync(dst2));

        // Close stream and verify locked destination preserved its original content
        lockStream.Close();
        Assert.Equal("OriginalLockedContent", await File.ReadAllTextAsync(dstLocked));
    }

    [Fact]
    public async Task SynchronizeAsync_MissingSource_CountedOnceAtSourceLevelNotMultipliedByDestinations()
    {
        var nonExistentSrc = Path.Combine(_testRoot, "does_not_exist.txt");
        var dst1 = Path.Combine(_testRoot, "d1.txt");
        var dst2 = Path.Combine(_testRoot, "d2.txt");
        var dst3 = Path.Combine(_testRoot, "d3.txt");

        var profile = new SyncProfile
        {
            Name = "MissingSourceProfile",
            Items =
            [
                new SyncItem
                {
                    Name = "ItemMissing",
                    Source = nonExistentSrc,
                    Destinations = [dst1, dst2, dst3]
                }
            ]
        };

        var report = await _engine.SynchronizeAsync(profile);

        // Missing source outcome is Source-level (counted 1 failure, not 3)
        Assert.Equal(1, report.FailedCount);
        Assert.Equal(0, report.CopiedCount);
        Assert.Equal(0, report.UnchangedCount);
        Assert.Single(report.Entries);
        Assert.Null(report.Entries[0].Destination);
    }

    [Fact]
    public async Task SynchronizeAsync_MixedStates_OneUnchangedOneCopied()
    {
        var src = Path.Combine(_testRoot, "source_mixed.txt");
        var dstExisting = Path.Combine(_testRoot, "dst_existing.txt");
        var dstNew = Path.Combine(_testRoot, "dst_new.txt");

        await File.WriteAllTextAsync(src, "ExactSameContent");
        await File.WriteAllTextAsync(dstExisting, "ExactSameContent");

        var sourceMtime = File.GetLastWriteTimeUtc(src);
        File.SetLastWriteTimeUtc(dstExisting, sourceMtime);

        var profile = new SyncProfile
        {
            Name = "MixedProfile",
            Items =
            [
                new SyncItem
                {
                    Name = "ItemMixed",
                    Source = src,
                    Destinations = [dstExisting, dstNew]
                }
            ]
        };

        var report = await _engine.SynchronizeAsync(profile);

        Assert.Equal(1, report.CopiedCount);
        Assert.Equal(1, report.UnchangedCount);
        Assert.Equal(0, report.FailedCount);
        Assert.Equal(2, report.Entries.Count);
        Assert.Equal("ExactSameContent", await File.ReadAllTextAsync(dstNew));
    }
}
