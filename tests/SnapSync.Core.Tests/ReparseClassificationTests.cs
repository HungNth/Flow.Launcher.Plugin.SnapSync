using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace SnapSync.Core.Tests;

public sealed class ReparseClassificationTests : IDisposable
{
    private readonly string _testRoot;
    private readonly SyncEngine _engine;

    public ReparseClassificationTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "SnapSync-reparse-test-" + Guid.NewGuid().ToString("N"));
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
    public void FileInfo_RegularFile_LinkTargetIsNull_NotClassifiedAsNavigationLink()
    {
        var filePath = Path.Combine(_testRoot, "regular.txt");
        File.WriteAllText(filePath, "RegularFilePayload");

        var fi = new FileInfo(filePath);
        Assert.Null(fi.LinkTarget);

        var isNav = PathSafety.ValidateNoNavigationLinks(filePath, out var invalidLink, out var err);
        Assert.True(isNav);
        Assert.Null(invalidLink);
        Assert.Null(err);
    }

    [Fact]
    public void DirectoryInfo_RegularDirectory_LinkTargetIsNull_NotClassifiedAsNavigationLink()
    {
        var dirPath = Path.Combine(_testRoot, "regular_folder");
        Directory.CreateDirectory(dirPath);

        var di = new DirectoryInfo(dirPath);
        Assert.Null(di.LinkTarget);

        var isNav = PathSafety.ValidateNoNavigationLinks(dirPath, out var invalidLink, out var err);
        Assert.True(isNav);
        Assert.Null(invalidLink);
        Assert.Null(err);
    }

    [Fact]
    public async Task SynchronizeAsync_OrdinaryFiles_NotMistakenForLinksEvenIfSparseOrArchive()
    {
        var srcDir = Path.Combine(_testRoot, "src");
        Directory.CreateDirectory(srcDir);
        var file = Path.Combine(srcDir, "doc.txt");
        await File.WriteAllTextAsync(file, "DocContent");

        // Set Archive or SparseFile attribute if supported
        File.SetAttributes(file, FileAttributes.Archive);

        var dstDir = Path.Combine(_testRoot, "dst");

        var profile = new SyncProfile
        {
            Name = "AttribProfile",
            Items =
            [
                new SyncItem
                {
                    Name = "Item1",
                    Source = srcDir,
                    Destinations = [dstDir],
                    ItemType = SyncItemType.Directory
                }
            ]
        };

        var report = await _engine.SynchronizeAsync(profile);

        Assert.Equal(1, report.CopiedCount);
        Assert.Equal(0, report.FailedCount);
        Assert.Equal("DocContent", await File.ReadAllTextAsync(Path.Combine(dstDir, "doc.txt")));
        Assert.DoesNotContain(report.Entries, e => e.Outcome == EntryOutcome.Skipped);
    }
}
