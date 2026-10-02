using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace SnapSync.Core.Tests;

public sealed class DirectoryExclusionsTests : IDisposable
{
    private readonly string _testRoot;
    private readonly SyncEngine _engine;

    public DirectoryExclusionsTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "SnapSync-dir-test-" + Guid.NewGuid().ToString("N"));
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
    public async Task SynchronizeAsync_DirectoryTreeWithEmptyDir_PreservesRelativeStructureAndEmptyDirs()
    {
        var srcDir = Path.Combine(_testRoot, "src");
        var subDir = Path.Combine(srcDir, "sub1", "sub2");
        var emptyDir = Path.Combine(srcDir, "emptyFolder");
        Directory.CreateDirectory(subDir);
        Directory.CreateDirectory(emptyDir);

        var file1 = Path.Combine(srcDir, "file1.txt");
        var file2 = Path.Combine(subDir, "file2.txt");
        await File.WriteAllTextAsync(file1, "RootFileContent");
        await File.WriteAllTextAsync(file2, "NestedFileContent");

        var dstDir = Path.Combine(_testRoot, "dst");

        var profile = new SyncProfile
        {
            Name = "DirProfile",
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

        var report = await _engine.SynchronizeAsync(profile);

        Assert.Equal(2, report.CopiedCount);
        Assert.Equal(0, report.FailedCount);

        // Verify files
        Assert.Equal("RootFileContent", await File.ReadAllTextAsync(Path.Combine(dstDir, "file1.txt")));
        Assert.Equal("NestedFileContent", await File.ReadAllTextAsync(Path.Combine(dstDir, "sub1", "sub2", "file2.txt")));

        // Verify empty directory preserved
        Assert.True(Directory.Exists(Path.Combine(dstDir, "emptyFolder")));
    }

    [Fact]
    public async Task SynchronizeAsync_UntouchedDestinationExtras_NeverDeletesOrMutatesExtraFiles()
    {
        var srcDir = Path.Combine(_testRoot, "src_extra");
        Directory.CreateDirectory(srcDir);
        await File.WriteAllTextAsync(Path.Combine(srcDir, "authoritative.txt"), "Authoritative");

        var dstDir = Path.Combine(_testRoot, "dst_extra");
        Directory.CreateDirectory(dstDir);
        var extraFile = Path.Combine(dstDir, "extra_destination_file.txt");
        await File.WriteAllTextAsync(extraFile, "PreserveMe");

        var profile = new SyncProfile
        {
            Name = "ExtraProfile",
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
        Assert.True(File.Exists(extraFile));
        Assert.Equal("PreserveMe", await File.ReadAllTextAsync(extraFile));
    }

    [Fact]
    public async Task SynchronizeAsync_ExclusionsPrunesSubtree_ProducesOneExcludedOutcome()
    {
        var srcDir = Path.Combine(_testRoot, "src_excl");
        var cacheDir = Path.Combine(srcDir, "cache");
        var cacheSubDir = Path.Combine(cacheDir, "nested");
        Directory.CreateDirectory(cacheSubDir);

        await File.WriteAllTextAsync(Path.Combine(srcDir, "keep.txt"), "KeepMe");
        await File.WriteAllTextAsync(Path.Combine(srcDir, "ignore.tmp"), "IgnoreMe");
        await File.WriteAllTextAsync(Path.Combine(cacheDir, "c1.bin"), "Cache1");
        await File.WriteAllTextAsync(Path.Combine(cacheSubDir, "c2.bin"), "Cache2");

        var dstDir = Path.Combine(_testRoot, "dst_excl");

        var profile = new SyncProfile
        {
            Name = "ExclProfile",
            Items =
            [
                new SyncItem
                {
                    Name = "ExclItem",
                    Source = srcDir,
                    Destinations = [dstDir],
                    ItemType = SyncItemType.Directory,
                    Exclusions = ["cache", "*.tmp"]
                }
            ]
        };

        var report = await _engine.SynchronizeAsync(profile);

        Assert.Equal(1, report.CopiedCount);
        Assert.Equal("KeepMe", await File.ReadAllTextAsync(Path.Combine(dstDir, "keep.txt")));
        Assert.False(File.Exists(Path.Combine(dstDir, "ignore.tmp")));
        Assert.False(Directory.Exists(Path.Combine(dstDir, "cache")));

        // Excluded entries
        Assert.Contains(report.Entries, e => e.Outcome == EntryOutcome.Excluded && e.Source.EndsWith("cache"));
        Assert.Contains(report.Entries, e => e.Outcome == EntryOutcome.Excluded && e.Source.EndsWith("ignore.tmp"));
    }

    [Fact]
    public async Task SynchronizeAsync_AutoMode_DetectsFileAndDirectoryCorrectly()
    {
        var srcFile = Path.Combine(_testRoot, "auto_file.txt");
        await File.WriteAllTextAsync(srcFile, "AutoFileContent");
        var dstFile = Path.Combine(_testRoot, "auto_target.txt");

        var srcDir = Path.Combine(_testRoot, "auto_dir");
        Directory.CreateDirectory(srcDir);
        await File.WriteAllTextAsync(Path.Combine(srcDir, "sub.txt"), "AutoDirContent");
        var dstDir = Path.Combine(_testRoot, "auto_dst_dir");

        var profile = new SyncProfile
        {
            Name = "AutoProfile",
            Items =
            [
                new SyncItem { Name = "ItemF", Source = srcFile, Destinations = [dstFile], ItemType = SyncItemType.Auto },
                new SyncItem { Name = "ItemD", Source = srcDir, Destinations = [dstDir], ItemType = SyncItemType.Auto }
            ]
        };

        var report = await _engine.SynchronizeAsync(profile);

        Assert.Equal(2, report.CopiedCount);
        Assert.Equal(0, report.FailedCount);
        Assert.Equal("AutoFileContent", await File.ReadAllTextAsync(dstFile));
        Assert.Equal("AutoDirContent", await File.ReadAllTextAsync(Path.Combine(dstDir, "sub.txt")));
    }

    [Fact]
    public async Task SynchronizeAsync_TypeMismatch_ReportsClearFailure()
    {
        var srcFile = Path.Combine(_testRoot, "real_file.txt");
        await File.WriteAllTextAsync(srcFile, "RealFile");

        var profile = new SyncProfile
        {
            Name = "MismatchProfile",
            Items =
            [
                new SyncItem
                {
                    Name = "MismatchItem",
                    Source = srcFile,
                    Destinations = [Path.Combine(_testRoot, "dst_mismatch")],
                    ItemType = SyncItemType.Directory // Mismatch: configured as Directory but is a File
                }
            ]
        };

        var report = await _engine.SynchronizeAsync(profile);

        Assert.Equal(0, report.CopiedCount);
        Assert.Equal(1, report.FailedCount);
        Assert.Contains(report.Entries, e => e.Outcome == EntryOutcome.Failed && e.ErrorMessage!.Contains("file, but item type is configured as Directory"));
    }
}
