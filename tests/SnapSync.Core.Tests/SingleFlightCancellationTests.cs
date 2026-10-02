using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SnapSync.Core.Tests;

public sealed class SingleFlightCancellationTests : IDisposable
{
    private readonly string _testRoot;
    private readonly SyncEngine _engine;

    public SingleFlightCancellationTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "SnapSync-cancel-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);
        _engine = new SyncEngine();
        SyncEngine.CopyProgressHook = null;
    }

    public void Dispose()
    {
        SyncEngine.CopyProgressHook = null;
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }

    [Fact]
    public async Task SynchronizeAsync_OperationCancelledImmediately_ReturnsPartialReportWithIsCancelledTrueAndNoTempFiles()
    {
        var srcFile = Path.Combine(_testRoot, "large_source.txt");
        var dstFile = Path.Combine(_testRoot, "target.txt");
        await File.WriteAllTextAsync(dstFile, "OriginalIntactDestination");

        var buffer = new byte[2 * 1024 * 1024];
        new Random(42).NextBytes(buffer);
        await File.WriteAllBytesAsync(srcFile, buffer);

        using var cts = new CancellationTokenSource();

        var profile = new SyncProfile
        {
            Name = "CancelProfile",
            Items = [new SyncItem { Name = "I1", Source = srcFile, Destinations = [dstFile] }]
        };

        // Cancel immediately
        cts.Cancel();

        var report = await _engine.SynchronizeAsync(profile, cts.Token);

        Assert.True(report.IsCancelled);
        Assert.False(report.IsSuccess);
        Assert.Equal("OriginalIntactDestination", await File.ReadAllTextAsync(dstFile));

        var tempFiles = Directory.GetFiles(_testRoot, "*.tmp.*");
        Assert.Empty(tempFiles);
    }

    [Fact]
    public async Task SynchronizeAsync_DeterministicCancellationViaHook_CopiesFirstItemThenCancelsSecond()
    {
        var src1 = Path.Combine(_testRoot, "src1.txt");
        var dst1 = Path.Combine(_testRoot, "dst1.txt");
        await File.WriteAllTextAsync(src1, "File1Content");

        var src2 = Path.Combine(_testRoot, "src2.txt");
        var dst2 = Path.Combine(_testRoot, "dst2.txt");
        await File.WriteAllTextAsync(src2, "File2Content");

        using var cts = new CancellationTokenSource();

        var profile = new SyncProfile
        {
            Name = "DeterministicCancelProfile",
            Items =
            [
                new SyncItem { Name = "I1", Source = src1, Destinations = [dst1] },
                new SyncItem { Name = "I2", Source = src2, Destinations = [dst2] }
            ]
        };

        // When item 1 completes, trigger cancellation deterministically
        SyncEngine.CopyProgressHook = async path =>
        {
            if (path == src1)
            {
                cts.Cancel();
            }
            await Task.CompletedTask;
        };

        var report = await _engine.SynchronizeAsync(profile, cts.Token);

        // Verification: Deterministically cancelled
        Assert.True(report.IsCancelled);
        Assert.False(report.IsSuccess);
        Assert.Equal(1, report.CopiedCount); // Item 1 succeeded
        Assert.True(File.Exists(dst1));
        Assert.Equal("File1Content", await File.ReadAllTextAsync(dst1));

        // Item 2 was never copied
        Assert.False(File.Exists(dst2));

        // Temporary files were cleaned up
        var tempFiles = Directory.GetFiles(_testRoot, "*.tmp.*");
        Assert.Empty(tempFiles);
    }

    [Fact]
    public async Task SynchronizeAsync_SourceMutatedDuringTransfer_DiscardsStagedCopyAndReportsFailure()
    {
        var srcFile = Path.Combine(_testRoot, "mutating_src.txt");
        var dstFile = Path.Combine(_testRoot, "mutating_dst.txt");
        await File.WriteAllTextAsync(srcFile, "InitialStableContent");
        await File.WriteAllTextAsync(dstFile, "OldTargetContent");

        var profile = new SyncProfile
        {
            Name = "MutateProfile",
            Items = [new SyncItem { Name = "I1", Source = srcFile, Destinations = [dstFile] }]
        };

        // Mutate source during transfer right after stream copy but before replacement
        SyncEngine.CopyProgressHook = async path =>
        {
            if (path == srcFile)
            {
                await File.AppendAllTextAsync(srcFile, "_MutatedPayload");
                File.SetLastWriteTimeUtc(srcFile, DateTime.UtcNow.AddMinutes(10));
            }
        };

        var report = await _engine.SynchronizeAsync(profile);

        // Verification: Failure reported without retrying
        Assert.Equal(0, report.CopiedCount);
        Assert.Equal(1, report.FailedCount);
        Assert.False(report.IsSuccess);

        // Destination was NOT overwritten with staged file
        Assert.Equal("OldTargetContent", await File.ReadAllTextAsync(dstFile));

        // Staged temp file was cleaned up
        var tempFiles = Directory.GetFiles(_testRoot, "*.tmp.*");
        Assert.Empty(tempFiles);
    }
}
