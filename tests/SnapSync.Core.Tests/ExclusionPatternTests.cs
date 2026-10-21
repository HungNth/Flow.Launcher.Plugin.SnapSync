using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace SnapSync.Core.Tests;

public sealed class ExclusionPatternTests
{
    [Theory]
    [InlineData("foo/**", "foo", true)]
    [InlineData("foo/**", "foo/bar.txt", false)]
    [InlineData("foo/**", "foo/sub/bar.txt", false)]
    [InlineData("**/cache/**", "cache", true)]
    [InlineData("**/cache/**", "sub/cache", true)]
    [InlineData("**/cache/**", "deep/nested/cache", true)]
    [InlineData("node_modules", "packages/node_modules", true)]
    [InlineData("cache", "deep/sub/cache", true)]
    public void IsExcluded_DirectoryPruning_MatchesPrefixes(string pattern, string relativePath, bool isDirectory)
    {
        var matcher = new ExclusionMatcher([pattern]);
        Assert.True(matcher.IsExcluded(relativePath, isDirectory: isDirectory));
    }

    [Theory]
    [InlineData("*.tmp", "test.tmp", true)]
    [InlineData("*.tmp", "test.TMP", true)]
    [InlineData("file?.txt", "file1.txt", true)]
    [InlineData("file?.txt", "fileA.txt", true)]
    [InlineData("file?.txt", "file12.txt", false)]
    [InlineData("docs/**/*.md", "docs/readme.md", true)]
    [InlineData("docs/**/*.md", "docs/sub/guide.md", true)]
    [InlineData("docs/**/*.md", "docs/a/b/c/page.md", true)]
    [InlineData("docs/**/*.md", "other/readme.md", false)]
    public void IsExcluded_WildcardsAndQuestionMarks_MatchesAccurately(string pattern, string relativePath, bool expected)
    {
        var matcher = new ExclusionMatcher([pattern]);
        Assert.Equal(expected, matcher.IsExcluded(relativePath, isDirectory: false));
    }

    [Fact]
    public void IsExcluded_BackslashAndForwardSlashSeparators_TreatedEquivalently()
    {
        var matcher = new ExclusionMatcher([@"folder\sub\*.log"]);
        Assert.True(matcher.IsExcluded("folder/sub/app.log"));
        Assert.True(matcher.IsExcluded(@"folder\sub\app.log"));
        Assert.True(matcher.IsExcluded("FOLDER/SUB/APP.LOG"));
    }

    [Theory]
    [InlineData("*.log", "sub/app.log", true)]
    [InlineData("*.log", "a/b/c/app.log", true)]
    [InlineData("*.json.bak", "config/settings.json.bak", true)]
    [InlineData("*.json.bak", "app.json.bak", true)]
    [InlineData("app.log", "sub/app.log", true)]
    [InlineData("sub/*.log", "sub/app.log", true)]
    [InlineData("sub/*.log", "other/sub/app.log", false)]
    public void IsExcluded_PatternWithoutSlash_MatchesAcrossAllSubdirectories(string pattern, string relativePath, bool expected)
    {
        var matcher = new ExclusionMatcher([pattern]);
        Assert.Equal(expected, matcher.IsExcluded(relativePath, isDirectory: false));
    }
}
