using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SnapSync.Core;

/// <summary>
/// Evaluates glob-like exclusion patterns against relative paths.
/// Supports '*', '?', and '**' with forward-slash '/' canonicalization, matching case-insensitively.
/// Provides directory-aware matching so patterns like 'foo/**', '**/cache/**', or 'dir/*' correctly prune directories.
/// </summary>
internal sealed class ExclusionMatcher
{
    private sealed record CompiledRule(Regex ExactRegex, Regex? DirPrefixRegex);

    private readonly List<CompiledRule> _rules = [];

    public ExclusionMatcher(IEnumerable<string>? patterns)
    {
        if (patterns == null) return;

        foreach (var pattern in patterns)
        {
            if (string.IsNullOrWhiteSpace(pattern)) continue;

            var trimmed = pattern.Trim();
            var canonical = trimmed.Replace('\\', '/').Trim('/');

            if (string.IsNullOrEmpty(canonical)) continue;

            var exactRegex = new Regex(ConvertGlobToRegex(canonical), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

            // Build directory-prefix regex for early directory pruning:
            // If pattern is "foo/**", then directory "foo" must match.
            // If pattern is "**/cache/**", then any directory ending in "/cache" or equal to "cache" must match.
            Regex? dirPrefixRegex = null;
            if (canonical.EndsWith("/**", StringComparison.Ordinal))
            {
                var dirPart = canonical[..^3].TrimEnd('/');
                if (!string.IsNullOrEmpty(dirPart))
                {
                    dirPrefixRegex = new Regex(ConvertGlobToRegex(dirPart), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
                }
            }
            else if (canonical.EndsWith("/*", StringComparison.Ordinal))
            {
                var dirPart = canonical[..^2].TrimEnd('/');
                if (!string.IsNullOrEmpty(dirPart))
                {
                    dirPrefixRegex = new Regex(ConvertGlobToRegex(dirPart), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
                }
            }

            _rules.Add(new CompiledRule(exactRegex, dirPrefixRegex));
        }
    }

    /// <summary>
    /// Checks whether the specified relative path matches any exclusion rule.
    /// </summary>
    /// <param name="relativePath">The relative path from the Source root (using forward or back slashes).</param>
    /// <param name="isDirectory">True if the path represents a directory that might be pruned early.</param>
    /// <returns>True if excluded, false otherwise.</returns>
    public bool IsExcluded(string relativePath, bool isDirectory = false)
    {
        if (_rules.Count == 0 || string.IsNullOrWhiteSpace(relativePath)) return false;

        var normalized = relativePath.Replace('\\', '/').Trim('/');
        if (string.IsNullOrEmpty(normalized)) return false;

        foreach (var rule in _rules)
        {
            if (rule.ExactRegex.IsMatch(normalized))
            {
                return true;
            }

            if (isDirectory && rule.DirPrefixRegex is not null && rule.DirPrefixRegex.IsMatch(normalized))
            {
                return true;
            }
        }

        return false;
    }

    private static string ConvertGlobToRegex(string pattern)
    {
        var sb = new System.Text.StringBuilder("^");
        var i = 0;
        var len = pattern.Length;

        while (i < len)
        {
            var c = pattern[i];

            if (c == '*')
            {
                if (i + 1 < len && pattern[i + 1] == '*')
                {
                    // '**'
                    if (i + 2 < len && pattern[i + 2] == '/')
                    {
                        // '**/': matches nothing or any directory prefix ending in '/'
                        sb.Append("(?:.*/)?");
                        i += 3;
                        continue;
                    }
                    else
                    {
                        // '**' alone: matches everything
                        sb.Append(".*");
                        i += 2;
                        continue;
                    }
                }
                else
                {
                    // Single '*' matches any chars except '/'
                    sb.Append("[^/]*");
                    i++;
                    continue;
                }
            }
            else if (c == '?')
            {
                // '?' matches single char except '/'
                sb.Append("[^/]");
                i++;
                continue;
            }
            else
            {
                sb.Append(Regex.Escape(c.ToString()));
                i++;
            }
        }

        sb.Append("$");
        return sb.ToString();
    }
}
