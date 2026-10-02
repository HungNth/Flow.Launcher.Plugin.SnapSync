using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace SnapSync.Core.Tests;

public sealed class FailureDiagnosticsTests : IDisposable
{
    private readonly string _testRoot;
    private readonly SyncEngine _engine;

    public FailureDiagnosticsTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "SnapSync-diag-test-" + Guid.NewGuid().ToString("N"));
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
    public async Task SynchronizeAsync_MissingSource_EmitsMissingSourceCategory()
    {
        var missingSrc = Path.Combine(_testRoot, "not_found.txt");
        var dst = Path.Combine(_testRoot, "dst.txt");

        var profile = new SyncProfile
        {
            Name = "DiagMissingProfile",
            Items = [new SyncItem { Name = "I1", Source = missingSrc, Destinations = [dst] }]
        };

        var report = await _engine.SynchronizeAsync(profile);

        Assert.Equal(1, report.FailedCount);
        Assert.Single(report.Entries);
        var entry = report.Entries[0];
        Assert.Equal(EntryOutcome.MissingSource, entry.Outcome);
        Assert.Equal(FailureCategory.MissingSource, entry.Category);
        Assert.NotNull(entry.ErrorMessage);
    }

    [Fact]
    public async Task SynchronizeAsync_LockedDestination_EmitsLockedFileCategory()
    {
        var src = Path.Combine(_testRoot, "src.txt");
        var dst = Path.Combine(_testRoot, "dst_locked.txt");
        await File.WriteAllTextAsync(src, "NewPayload");
        await File.WriteAllTextAsync(dst, "LockedPayload");

        using var lockStream = new FileStream(dst, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var profile = new SyncProfile
        {
            Name = "DiagLockedProfile",
            Items = [new SyncItem { Name = "I1", Source = src, Destinations = [dst] }]
        };

        var report = await _engine.SynchronizeAsync(profile);

        Assert.Equal(1, report.FailedCount);
        Assert.Single(report.Entries);
        var entry = report.Entries[0];
        Assert.Equal(EntryOutcome.Failed, entry.Outcome);
        Assert.Equal(FailureCategory.LockedFile, entry.Category);
    }

    [Fact]
    public async Task SynchronizeAsync_TypeMismatch_EmitsValidationErrorCategory()
    {
        var srcFile = Path.Combine(_testRoot, "a_file.txt");
        await File.WriteAllTextAsync(srcFile, "Payload");

        var profile = new SyncProfile
        {
            Name = "DiagMismatchProfile",
            Items =
            [
                new SyncItem
                {
                    Name = "MismatchItem",
                    Source = srcFile,
                    Destinations = [Path.Combine(_testRoot, "dst_folder")],
                    ItemType = SyncItemType.Directory
                }
            ]
        };

        var report = await _engine.SynchronizeAsync(profile);

        Assert.Equal(1, report.FailedCount);
        Assert.Single(report.Entries);
        var entry = report.Entries[0];
        Assert.Equal(EntryOutcome.Failed, entry.Outcome);
        Assert.Equal(FailureCategory.ValidationError, entry.Category);
    }

    [Fact]
    public void ExceptionClassifier_AccessDeniedVsSharingViolation_DistinguishedByHResult()
    {
        // ERROR_ACCESS_DENIED (5) -> HResult: 0x80070005
        var accessDeniedEx = new UnauthorizedAccessException("Access denied", new Exception { HResult = unchecked((int)0x80070005) });
        Assert.Equal(FailureCategory.PermissionDenied, ExceptionClassifier.Classify(accessDeniedEx));

        // ERROR_SHARING_VIOLATION (32) -> HResult: 0x80070020
        var sharingViolationEx = new IOException("Sharing violation", unchecked((int)0x80070020));
        Assert.Equal(FailureCategory.LockedFile, ExceptionClassifier.Classify(sharingViolationEx));

        // ERROR_DISK_FULL (112) -> HResult: 0x80070070
        var diskFullEx = new IOException("Disk full", unchecked((int)0x80070070));
        Assert.Equal(FailureCategory.DiskFull, ExceptionClassifier.Classify(diskFullEx));
    }
}
