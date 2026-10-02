using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace SnapSync.Core.Tests;

public sealed class PreviewAndCachedReportsTests : IDisposable
{
    private readonly string _testRoot;
    private readonly SyncEngine _engine;

    public PreviewAndCachedReportsTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "SnapSync-preview-test-" + Guid.NewGuid().ToString("N"));
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
    public async Task PreviewAsync_DoesNotMutateDestinationFilesOrDirectories()
    {
        var srcDir = Path.Combine(_testRoot, "src_preview");
        var subDir = Path.Combine(srcDir, "sub");
        Directory.CreateDirectory(subDir);
        var srcFile = Path.Combine(subDir, "file.txt");
        await File.WriteAllTextAsync(srcFile, "PreviewContent");

        var dstDir = Path.Combine(_testRoot, "dst_preview");

        var profile = new SyncProfile
        {
            Name = "PreviewProfile",
            Items =
            [
                new SyncItem
                {
                    Name = "DirItem",
                    Source = srcDir,
                    Destinations = [dstDir],
                    ItemType = SyncItemType.Directory
                }
            ]
        };

        var previewReport = await _engine.PreviewAsync(profile);

        Assert.Equal(1, previewReport.CopiedCount);
        Assert.Equal(0, previewReport.FailedCount);

        // Verification: Zero filesystem mutations
        Assert.False(Directory.Exists(dstDir), "Preview must not create destination root.");
        Assert.False(File.Exists(Path.Combine(dstDir, "sub", "file.txt")), "Preview must not create destination files.");

        // Verification: Outlines WouldCopy and WouldCreateDirectory
        Assert.Contains(previewReport.Entries, e => e.Outcome == EntryOutcome.WouldCreateDirectory);
        Assert.Contains(previewReport.Entries, e => e.Outcome == EntryOutcome.WouldCopy);
    }

    [Fact]
    public async Task PreviewAsync_FollowedBySynchronizeAsync_RecomputesFreshly()
    {
        var srcFile = Path.Combine(_testRoot, "file1.txt");
        await File.WriteAllTextAsync(srcFile, "InitialVersion");
        var dstFile = Path.Combine(_testRoot, "file1_dst.txt");

        var profile = new SyncProfile
        {
            Name = "RecomputeProfile",
            Items = [new SyncItem { Name = "F1", Source = srcFile, Destinations = [dstFile] }]
        };

        // 1. Run preview
        var previewReport = await _engine.PreviewAsync(profile);
        Assert.Equal(1, previewReport.CopiedCount);
        Assert.False(File.Exists(dstFile));

        // 2. Mutate source before actual synchronization
        await File.WriteAllTextAsync(srcFile, "FreshlyMutatedVersion");

        // 3. Synchronize
        var syncReport = await _engine.SynchronizeAsync(profile);
        Assert.Equal(1, syncReport.CopiedCount);
        Assert.Equal(0, syncReport.FailedCount);
        Assert.Equal("FreshlyMutatedVersion", await File.ReadAllTextAsync(dstFile));
    }

    [Fact]
    public async Task PreviewAsync_ReportDetailsCappedAt500PrioritizingActionableOutcomes()
    {
        var srcDir = Path.Combine(_testRoot, "src_large");
        Directory.CreateDirectory(srcDir);

        // Create 600 files
        for (var i = 0; i < 600; i++)
        {
            await File.WriteAllTextAsync(Path.Combine(srcDir, $"file_{i:D4}.txt"), $"Content_{i}");
        }

        var dstDir = Path.Combine(_testRoot, "dst_large");

        var profile = new SyncProfile
        {
            Name = "LargeProfile",
            Items =
            [
                new SyncItem
                {
                    Name = "LargeItem",
                    Source = srcDir,
                    Destinations = [dstDir],
                    ItemType = SyncItemType.Directory
                }
            ]
        };

        var report = await _engine.PreviewAsync(profile);

        // Aggregate counters are exact even when details are truncated
        Assert.Equal(600, report.CopiedCount);
        Assert.True(report.IsTruncated);
        Assert.Equal(500, report.Entries.Count);
        Assert.True(report.TotalDiscoveredCount >= 600);
    }
}
