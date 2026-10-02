using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace SnapSync.Core.Tests;

public sealed class NavigationLinkSkipTests : IDisposable
{
    private readonly string _testRoot;
    private readonly SyncEngine _engine;

    public NavigationLinkSkipTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "SnapSync-navlink-test-" + Guid.NewGuid().ToString("N"));
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
    public async Task SynchronizeAsync_NestedDirectoryJunction_IsSkippedAndNotFollowed()
    {
        var srcDir = Path.Combine(_testRoot, "src");
        Directory.CreateDirectory(srcDir);
        await File.WriteAllTextAsync(Path.Combine(srcDir, "valid.txt"), "ValidContent");

        var outsideTargetDir = Path.Combine(_testRoot, "outside_target");
        Directory.CreateDirectory(outsideTargetDir);
        await File.WriteAllTextAsync(Path.Combine(outsideTargetDir, "secret.txt"), "SecretContent");

        var junctionPath = Path.Combine(srcDir, "junction_folder");
        var junctionCreated = false;

        try
        {
            Directory.CreateSymbolicLink(junctionPath, outsideTargetDir);
            junctionCreated = true;
        }
        catch
        {
            // If OS privilege lacks symlink creation, try junction creation or skip test execution gracefully
        }

        if (!junctionCreated)
        {
            return;
        }

        var dstDir = Path.Combine(_testRoot, "dst");

        var profile = new SyncProfile
        {
            Name = "NavProfile",
            Items =
            [
                new SyncItem
                {
                    Name = "NavItem",
                    Source = srcDir,
                    Destinations = [dstDir],
                    ItemType = SyncItemType.Directory
                }
            ]
        };

        var report = await _engine.SynchronizeAsync(profile);

        // Valid file is copied
        Assert.Equal(1, report.CopiedCount);
        Assert.Equal("ValidContent", await File.ReadAllTextAsync(Path.Combine(dstDir, "valid.txt")));

        // Junction folder must be skipped and NOT traversed into dst
        Assert.False(File.Exists(Path.Combine(dstDir, "junction_folder", "secret.txt")));
        Assert.Contains(report.Entries, e => e.Outcome == EntryOutcome.Skipped && e.Source == junctionPath);
    }
}
