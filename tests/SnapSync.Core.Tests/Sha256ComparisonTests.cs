using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace SnapSync.Core.Tests;

public sealed class Sha256ComparisonTests : IDisposable
{
    private readonly string _testRoot;
    private readonly SyncEngine _engine;

    public Sha256ComparisonTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "SnapSync-sha256-test-" + Guid.NewGuid().ToString("N"));
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
    public async Task SynchronizeAsync_EqualContentDifferentTimestamps_ProducesUnchangedAndNoMetadataWrites()
    {
        var srcFile = Path.Combine(_testRoot, "src.txt");
        var dstFile = Path.Combine(_testRoot, "dst.txt");
        await File.WriteAllTextAsync(srcFile, "EqualContentAcrossBothSides");
        await File.WriteAllTextAsync(dstFile, "EqualContentAcrossBothSides");

        var oldDestTime = DateTime.UtcNow.AddDays(-1);
        File.SetLastWriteTimeUtc(dstFile, oldDestTime);

        var profile = new SyncProfile
        {
            Name = "ShaProfile",
            Items =
            [
                new SyncItem
                {
                    Name = "Item1",
                    Source = srcFile,
                    Destinations = [dstFile],
                    ComparisonMode = ComparisonMode.Sha256
                }
            ]
        };

        var report = await _engine.SynchronizeAsync(profile);

        Assert.Equal(0, report.CopiedCount);
        Assert.Equal(1, report.UnchangedCount);
        Assert.Equal(0, report.FailedCount);

        var actualDestTime = File.GetLastWriteTimeUtc(dstFile);
        Assert.Equal(oldDestTime, actualDestTime);
    }

    [Fact]
    public async Task SynchronizeAsync_EqualLengthDifferentContent_ProducesCopiedUnderSha256()
    {
        var srcFile = Path.Combine(_testRoot, "src_diff.txt");
        var dstFile = Path.Combine(_testRoot, "dst_diff.txt");
        await File.WriteAllTextAsync(srcFile, "1234567890");
        await File.WriteAllTextAsync(dstFile, "0987654321");

        var commonTime = DateTime.UtcNow;
        File.SetLastWriteTimeUtc(srcFile, commonTime);
        File.SetLastWriteTimeUtc(dstFile, commonTime);

        var profile = new SyncProfile
        {
            Name = "ShaDiffProfile",
            Items =
            [
                new SyncItem
                {
                    Name = "Item1",
                    Source = srcFile,
                    Destinations = [dstFile],
                    ComparisonMode = ComparisonMode.Sha256
                }
            ]
        };

        var report = await _engine.SynchronizeAsync(profile);

        Assert.Equal(1, report.CopiedCount);
        Assert.Equal(0, report.UnchangedCount);
        Assert.Equal("1234567890", await File.ReadAllTextAsync(dstFile));
    }

    [Fact]
    public async Task PreviewAsync_EqualLengthDifferentContent_ProducesWouldCopyUnderSha256()
    {
        var srcFile = Path.Combine(_testRoot, "src_prev.txt");
        var dstFile = Path.Combine(_testRoot, "dst_prev.txt");
        await File.WriteAllTextAsync(srcFile, "AAAAA");
        await File.WriteAllTextAsync(dstFile, "BBBBB");

        var profile = new SyncProfile
        {
            Name = "ShaPreviewProfile",
            Items =
            [
                new SyncItem
                {
                    Name = "Item1",
                    Source = srcFile,
                    Destinations = [dstFile],
                    ComparisonMode = ComparisonMode.Sha256
                }
            ]
        };

        var report = await _engine.PreviewAsync(profile);

        Assert.Equal(1, report.CopiedCount);
        Assert.Contains(report.Entries, e => e.Outcome == EntryOutcome.WouldCopy);
        Assert.Equal("BBBBB", await File.ReadAllTextAsync(dstFile));
    }

    [Fact]
    public async Task SynchronizeAsync_Sha256MultipleDestinations_OneUnchangedOneCopied()
    {
        var srcFile = Path.Combine(_testRoot, "src_multi.txt");
        var dstMatch = Path.Combine(_testRoot, "dst_match.txt");
        var dstDiff = Path.Combine(_testRoot, "dst_diff.txt");

        await File.WriteAllTextAsync(srcFile, "SharedSourcePayload");
        await File.WriteAllTextAsync(dstMatch, "SharedSourcePayload");
        await File.WriteAllTextAsync(dstDiff, "DifferentTargetPayload");

        var profile = new SyncProfile
        {
            Name = "ShaMultiDst",
            Items =
            [
                new SyncItem
                {
                    Name = "ItemMulti",
                    Source = srcFile,
                    Destinations = [dstMatch, dstDiff],
                    ComparisonMode = ComparisonMode.Sha256
                }
            ]
        };

        var report = await _engine.SynchronizeAsync(profile);

        Assert.Equal(1, report.CopiedCount);
        Assert.Equal(1, report.UnchangedCount);
        Assert.Equal(0, report.FailedCount);
        Assert.Equal("SharedSourcePayload", await File.ReadAllTextAsync(dstDiff));
    }

    [Fact]
    public async Task SynchronizeAsync_Sha256DirectoryMode_ComparesAndCopiesAccurately()
    {
        var srcDir = Path.Combine(_testRoot, "src_dir");
        Directory.CreateDirectory(srcDir);
        var f1 = Path.Combine(srcDir, "file1.txt");
        var f2 = Path.Combine(srcDir, "file2.txt");
        await File.WriteAllTextAsync(f1, "HashContent1");
        await File.WriteAllTextAsync(f2, "HashContent2");

        var dstDir = Path.Combine(_testRoot, "dst_dir");
        Directory.CreateDirectory(dstDir);
        await File.WriteAllTextAsync(Path.Combine(dstDir, "file1.txt"), "HashContent1");
        await File.WriteAllTextAsync(Path.Combine(dstDir, "file2.txt"), "OldHashContent2");

        var profile = new SyncProfile
        {
            Name = "ShaDirProfile",
            Items =
            [
                new SyncItem
                {
                    Name = "DirItem",
                    Source = srcDir,
                    Destinations = [dstDir],
                    ItemType = SyncItemType.Directory,
                    ComparisonMode = ComparisonMode.Sha256
                }
            ]
        };

        var report = await _engine.SynchronizeAsync(profile);

        Assert.Equal(1, report.CopiedCount);
        Assert.Equal(1, report.UnchangedCount);
        Assert.Equal(0, report.FailedCount);
        Assert.Equal("HashContent2", await File.ReadAllTextAsync(Path.Combine(dstDir, "file2.txt")));
    }
}
