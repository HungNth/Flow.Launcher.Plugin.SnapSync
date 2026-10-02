using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace SnapSync.Core.Tests;

/// <summary>
/// Data-driven and real-filesystem tests for portable, conflict-safe paths and structural preflight validation
/// exercising the public <see cref="ISyncEngine"/> seam.
/// </summary>
public sealed class PathSafetyTests : IDisposable
{
    private readonly string _testRoot;
    private readonly ISyncEngine _engine;

    public PathSafetyTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "SnapSync_PathSafetyTests_" + Guid.NewGuid().ToString("N"));
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
            // Best effort cleanup in tests
        }
    }

    [Theory]
    [InlineData("/")]
    [InlineData("\\")]
    public async Task SynchronizeAsync_HomeRelativeSource_CopiesExpandedFile(string separator)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var folderName = ".SnapSync-home-test-" + Guid.NewGuid().ToString("N");
        var sourceDirectory = Path.Combine(home, folderName);
        Directory.CreateDirectory(sourceDirectory);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(sourceDirectory, "source.txt"), "home-relative content");
            var destination = Path.Combine(_testRoot, "target.txt");
            var profile = new SyncProfile
            {
                Name = "Home",
                Items = [new SyncItem
                {
                    Name = "HomeItem",
                    Source = $"~{separator}{folderName}{separator}source.txt",
                    Destinations = [destination]
                }]
            };

            var report = await _engine.SynchronizeAsync(profile);

            Assert.Equal(1, report.CopiedCount);
            Assert.Equal(0, report.FailedCount);
            Assert.Equal("home-relative content", await File.ReadAllTextAsync(destination));
        }
        finally
        {
            Directory.Delete(sourceDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task SynchronizeAsync_LiteralPercentInFilename_CopiesWithoutTreatingItAsVariable()
    {
        var source = Path.Combine(_testRoot, "100% complete.txt");
        var destination = Path.Combine(_testRoot, "target.txt");
        await File.WriteAllTextAsync(source, "literal percent content");
        var profile = new SyncProfile
        {
            Name = "Percent",
            Items = [new SyncItem { Name = "PercentItem", Source = source, Destinations = [destination] }]
        };

        var report = await _engine.SynchronizeAsync(profile);

        Assert.Equal(1, report.CopiedCount);
        Assert.Equal(0, report.FailedCount);
        Assert.Equal("literal percent content", await File.ReadAllTextAsync(destination));
    }

    [Fact]
    public async Task SynchronizeAsync_EnvironmentVariable_ExpandsFreshlyBetweenOperations()
    {
        var envVar = "SNAPSYNC_TEST_VAR_" + Guid.NewGuid().ToString("N");
        var srcDir1 = Path.Combine(_testRoot, "src1");
        Directory.CreateDirectory(srcDir1);
        var file1 = Path.Combine(srcDir1, "test.txt");
        await File.WriteAllTextAsync(file1, "FirstVersionContent");

        var srcDir2 = Path.Combine(_testRoot, "src2");
        Directory.CreateDirectory(srcDir2);
        var file2 = Path.Combine(srcDir2, "test.txt");
        await File.WriteAllTextAsync(file2, "SecondVersionContent");

        var profile = new SyncProfile
        {
            Name = "DynamicEnvProfile",
            Items =
            [
                new SyncItem
                {
                    Name = "Item1",
                    Source = $"%{envVar}%/test.txt",
                    Destinations = [$"%{envVar}%/target.txt"]
                }
            ]
        };

        try
        {
            // First operation with envVar pointing to srcDir1
            Environment.SetEnvironmentVariable(envVar, srcDir1);
            var report1 = await _engine.SynchronizeAsync(profile);

            Assert.True(report1.IsSuccess);
            Assert.Equal("FirstVersionContent", await File.ReadAllTextAsync(Path.Combine(srcDir1, "target.txt")));

            // Mutate the same variable to point to srcDir2
            Environment.SetEnvironmentVariable(envVar, srcDir2);
            var report2 = await _engine.SynchronizeAsync(profile);

            Assert.True(report2.IsSuccess);
            Assert.Equal("SecondVersionContent", await File.ReadAllTextAsync(Path.Combine(srcDir2, "target.txt")));
            Assert.Equal("FirstVersionContent", await File.ReadAllTextAsync(Path.Combine(srcDir1, "target.txt")));
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVar, null);
        }
    }

    [Fact]
    public void Validate_UnresolvedEnvironmentVariable_RejectsWithSourceValidationError()
    {
        var profile = new SyncProfile
        {
            Name = "UnresolvedEnvProfile",
            Items =
            [
                new SyncItem
                {
                    Name = "Item1",
                    Source = $"%SNAPSYNC_MISSING_{Guid.NewGuid():N}%/file.txt",
                    Destinations = [Path.Combine(_testRoot, "dst.txt")]
                }
            ]
        };

        var errors = _engine.Validate(profile);

        Assert.Contains(errors, e => e.PropertyName == nameof(SyncItem.Source));
    }

    [Theory]
    [InlineData("relative/path/file.txt")]
    [InlineData(@"..\relative\path.txt")]
    [InlineData("just_filename.txt")]
    [InlineData(@"C:relativeWithoutSlash.txt")]
    [InlineData(@"\\server")]
    [InlineData(@"\\server\")]
    [InlineData(@"\\?\C:\device\path.txt")]
    [InlineData(@"\\.\pipe\test.txt")]
    public void Validate_RelativeOrNonRootedPath_RejectsWithValidationError(string invalidPath)
    {
        var profile = new SyncProfile
        {
            Name = "InvalidPathProfile",
            Items =
            [
                new SyncItem
                {
                    Name = "Item1",
                    Source = invalidPath,
                    Destinations = [Path.Combine(_testRoot, "dst.txt")]
                }
            ]
        };

        var errors = _engine.Validate(profile);

        Assert.Contains(errors, e => e.PropertyName == nameof(SyncItem.Source));
    }

    [Theory]
    [InlineData(@"\\server\share\folder\file.txt")]
    [InlineData(@"//server/share/folder/file.txt")]
    public void Validate_ValidUncPath_IsAccepted(string uncPath)
    {
        var profile = new SyncProfile
        {
            Name = "UncProfile",
            Items =
            [
                new SyncItem
                {
                    Name = "Item1",
                    Source = uncPath,
                    Destinations = [Path.Combine(_testRoot, "dst.txt")]
                }
            ]
        };

        var errors = _engine.Validate(profile);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_CaseInsensitiveSameSourceAndDestination_RejectsWithDestinationValidationError()
    {
        var file1 = Path.Combine(_testRoot, "FILE.txt");
        var file2 = Path.Combine(_testRoot, "file.TXT");
        var profile = new SyncProfile
        {
            Name = "IdenticalPathProfile",
            Items =
            [
                new SyncItem
                {
                    Name = "Item1",
                    Source = file1,
                    Destinations = [file2]
                }
            ]
        };

        var errors = _engine.Validate(profile);

        Assert.Contains(errors, e => e.PropertyName == nameof(SyncItem.Destinations));
    }

    [Fact]
    public void Validate_DuplicateDestinations_RejectsWithDestinationValidationError()
    {
        var source = Path.Combine(_testRoot, "source.txt");
        var dest1 = Path.Combine(_testRoot, "dest.txt");
        var dest2 = Path.Combine(_testRoot, "DEST.TXT");
        var profile = new SyncProfile
        {
            Name = "DuplicateDestProfile",
            Items =
            [
                new SyncItem
                {
                    Name = "Item1",
                    Source = source,
                    Destinations = [dest1, dest2]
                }
            ]
        };

        var errors = _engine.Validate(profile);

        Assert.Contains(errors, e => e.PropertyName == nameof(SyncItem.Destinations));
    }

    [Fact]
    public void Validate_SourceInsideDestination_RejectsWithDestinationValidationError()
    {
        var destDir = Path.Combine(_testRoot, "backup");
        var sourceFile = Path.Combine(destDir, "source.txt");

        var profile = new SyncProfile
        {
            Name = "ContainmentProfile",
            Items =
            [
                new SyncItem
                {
                    Name = "Item1",
                    Source = sourceFile,
                    Destinations = [destDir]
                }
            ]
        };

        var errors = _engine.Validate(profile);

        Assert.Contains(errors, e => e.PropertyName == nameof(SyncItem.Destinations));
    }

    [Fact]
    public void Validate_DestinationInsideSource_RejectsWithDestinationValidationError()
    {
        var sourceDir = Path.Combine(_testRoot, "source_folder");
        var destDir = Path.Combine(sourceDir, "sub_dest");

        var profile = new SyncProfile
        {
            Name = "ContainmentProfile",
            Items =
            [
                new SyncItem
                {
                    Name = "Item1",
                    Source = sourceDir,
                    Destinations = [destDir]
                }
            ]
        };

        var errors = _engine.Validate(profile);

        Assert.Contains(errors, e => e.PropertyName == nameof(SyncItem.Destinations));
    }

    [Fact]
    public void Validate_BoundarySiblings_AreAcceptedWithoutContainmentError()
    {
        var sourceDir = Path.Combine(_testRoot, "folder");
        var destDir = Path.Combine(_testRoot, "folder_backup");

        var profile = new SyncProfile
        {
            Name = "BoundaryProfile",
            Items =
            [
                new SyncItem
                {
                    Name = "Item1",
                    Source = Path.Combine(sourceDir, "file.txt"),
                    Destinations = [Path.Combine(destDir, "file.txt")]
                }
            ]
        };

        var errors = _engine.Validate(profile);

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateConfiguration_CrossItemWriteWriteOverlap_RejectsWithDestinationValidationError()
    {
        var targetFile = Path.Combine(_testRoot, "dest", "target.txt");
        var config = new SnapSyncConfiguration
        {
            Profiles =
            [
                new SyncProfile
                {
                    Name = "Profile1",
                    Enabled = true,
                    Items =
                    [
                        new SyncItem
                        {
                            Name = "Item1",
                            Source = Path.Combine(_testRoot, "src1.txt"),
                            Destinations = [targetFile]
                        }
                    ]
                },
                new SyncProfile
                {
                    Name = "Profile2",
                    Enabled = true,
                    Items =
                    [
                        new SyncItem
                        {
                            Name = "Item2",
                            Source = Path.Combine(_testRoot, "src2.txt"),
                            Destinations = [targetFile]
                        }
                    ]
                }
            ]
        };

        var errors = _engine.Validate(config);

        Assert.Contains(errors, e => e.PropertyName == nameof(SyncItem.Destinations));
    }

    [Fact]
    public void ValidateConfiguration_CrossItemReadWriteOverlap_RejectsWithDestinationValidationError()
    {
        var intermediateFile = Path.Combine(_testRoot, "shared.txt");
        var config = new SnapSyncConfiguration
        {
            Profiles =
            [
                new SyncProfile
                {
                    Name = "Profile1",
                    Enabled = true,
                    Items =
                    [
                        new SyncItem
                        {
                            Name = "Item1",
                            Source = Path.Combine(_testRoot, "initial.txt"),
                            Destinations = [intermediateFile]
                        }
                    ]
                },
                new SyncProfile
                {
                    Name = "Profile2",
                    Enabled = true,
                    Items =
                    [
                        new SyncItem
                        {
                            Name = "Item2",
                            Source = intermediateFile,
                            Destinations = [Path.Combine(_testRoot, "final.txt")]
                        }
                    ]
                }
            ]
        };

        var errors = _engine.Validate(config);

        Assert.Contains(errors, e => e.PropertyName == nameof(SyncItem.Destinations));
    }

    [Fact]
    public void ValidateConfiguration_DisabledProfileItems_DoNotCauseCrossConflict()
    {
        var targetFile = Path.Combine(_testRoot, "dest", "target.txt");
        var config = new SnapSyncConfiguration
        {
            Profiles =
            [
                new SyncProfile
                {
                    Name = "Profile1",
                    Enabled = true,
                    Items =
                    [
                        new SyncItem
                        {
                            Name = "Item1",
                            Source = Path.Combine(_testRoot, "src1.txt"),
                            Destinations = [targetFile]
                        }
                    ]
                },
                new SyncProfile
                {
                    Name = "Profile2",
                    Enabled = false, // Disabled profile
                    Items =
                    [
                        new SyncItem
                        {
                            Name = "Item2",
                            Source = Path.Combine(_testRoot, "src2.txt"),
                            Destinations = [targetFile]
                        }
                    ]
                }
            ]
        };

        var errors = _engine.Validate(config);

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateConfiguration_DisabledItem_MustStillBeInternallyValid()
    {
        var config = new SnapSyncConfiguration
        {
            Profiles =
            [
                new SyncProfile
                {
                    Name = "Profile1",
                    Enabled = true,
                    Items =
                    [
                        new SyncItem
                        {
                            Name = "ValidItem",
                            Source = Path.Combine(_testRoot, "src.txt"),
                            Destinations = [Path.Combine(_testRoot, "dst.txt")]
                        },
                        new SyncItem
                        {
                            Name = "DisabledInvalidItem",
                            Enabled = false,
                            Source = "relative/invalid/path.txt",
                            Destinations = [Path.Combine(_testRoot, "dst2.txt")]
                        }
                    ]
                }
            ]
        };

        var errors = _engine.Validate(config);

        Assert.Contains(errors, e => e.PropertyName == nameof(SyncItem.Source));
    }

    [Fact]
    public async Task SynchronizeAsync_WhenStructuralValidationErrorExists_AbortsWithoutAnyWrites()
    {
        var srcFile = Path.Combine(_testRoot, "src.txt");
        await File.WriteAllTextAsync(srcFile, "AuthoritativeContent");
        var dstFile = Path.Combine(_testRoot, "dst.txt");

        var profile = new SyncProfile
        {
            Name = "MixedProfile",
            Items =
            [
                new SyncItem
                {
                    Name = "ValidItem",
                    Source = srcFile,
                    Destinations = [dstFile]
                },
                new SyncItem
                {
                    Name = "InvalidItem",
                    Source = srcFile,
                    Destinations = [srcFile] // Identical source/destination
                }
            ]
        };

        var report = await _engine.SynchronizeAsync(profile);

        Assert.False(report.IsSuccess);
        Assert.False(File.Exists(dstFile));
        Assert.Equal(0, report.CopiedCount);
    }

    [Fact]
    public void GetAvailabilityWarnings_WhenSourceMissing_ReportsWarningWhileStructuralValidationPasses()
    {
        var missingSource = Path.Combine(_testRoot, "non_existent_source.txt");
        var dest = Path.Combine(_testRoot, "dst.txt");
        var profile = new SyncProfile
        {
            Name = "MissingSourceProfile",
            Items =
            [
                new SyncItem
                {
                    Name = "Item1",
                    Source = missingSource,
                    Destinations = [dest]
                }
            ]
        };

        var errors = _engine.Validate(profile);
        Assert.Empty(errors);

        var warnings = _engine.GetAvailabilityWarnings(profile);
        Assert.NotEmpty(warnings);
    }

    [Fact]
    public void Validate_WhenNavigationLinkAncestorExists_RejectsWithValidationError()
    {
        var targetDir = Path.Combine(_testRoot, "real_folder");
        Directory.CreateDirectory(targetDir);
        var linkDir = Path.Combine(_testRoot, "link_folder");

        var linkCreated = false;
        try
        {
            Directory.CreateSymbolicLink(linkDir, targetDir);
            linkCreated = true;
        }
        catch
        {
            // Skip link creation if runner lacks symlink privileges
        }

        if (linkCreated)
        {
            var pathInsideLink = Path.Combine(linkDir, "subfile.txt");
            var profile = new SyncProfile
            {
                Name = "SymlinkProfile",
                Items =
                [
                    new SyncItem
                    {
                        Name = "Item1",
                        Source = pathInsideLink,
                        Destinations = [Path.Combine(_testRoot, "dst.txt")]
                    }
                ]
            };

            var errors = _engine.Validate(profile);
            Assert.Contains(errors, e => e.PropertyName == nameof(SyncItem.Source));
        }
    }
}
