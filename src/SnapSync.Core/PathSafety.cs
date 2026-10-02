using System;
using System.IO;
using System.Text.RegularExpressions;

namespace SnapSync.Core;

/// <summary>
/// Internal helper for path expansion, validation, normalization, and conflict checking logic.
/// </summary>
internal static class PathSafety
{
    private static readonly Regex EnvironmentVariableRegex = new(@"%([^%]+)%", RegexOptions.Compiled);

    /// <summary>
    /// Expands leading '~' to the user's home profile directory and expands %VAR% environment variables.
    /// Rejects unresolved environment-variable expressions while preserving literal percent characters.
    /// </summary>
    internal static bool TryExpandPath(string path, out string? expandedPath, out string? errorMessage)
    {
        expandedPath = null;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(path))
        {
            errorMessage = "Path cannot be empty.";
            return false;
        }

        var trimmed = path.Trim();

        // 1. Expand leading ~
        if (trimmed == "~" || trimmed.StartsWith("~/", StringComparison.Ordinal) || trimmed.StartsWith("~\\", StringComparison.Ordinal))
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            if (string.IsNullOrWhiteSpace(userProfile))
            {
                errorMessage = "Cannot resolve user home directory for '~'.";
                return false;
            }

            if (trimmed == "~")
            {
                trimmed = userProfile;
            }
            else
            {
                trimmed = Path.Combine(userProfile, trimmed[2..]);
            }
        }

        var expanded = Environment.ExpandEnvironmentVariables(trimmed);
        var unresolvedVariable = EnvironmentVariableRegex.Match(expanded);
        if (unresolvedVariable.Success)
        {
            errorMessage = $"Unresolved environment variable: {unresolvedVariable.Value}.";
            return false;
        }

        expandedPath = expanded;
        return true;
    }

    /// <summary>
    /// Validates and fully normalizes a path into a canonical rooted Windows drive or UNC path.
    /// Requires fully rooted input before calling <see cref="Path.GetFullPath(string)"/> to prevent resolving against cwd.
    /// </summary>
    internal static bool TryNormalizePath(string path, out string? normalizedPath, out string? errorMessage)
    {
        normalizedPath = null;
        errorMessage = null;

        if (!TryExpandPath(path, out var expanded, out errorMessage) || expanded is null)
        {
            return false;
        }

        // Must be fully rooted Windows drive or UNC path BEFORE calling GetFullPath
        if (!IsRootedDriveOrUnc(expanded))
        {
            errorMessage = $"Path must be a fully rooted drive (e.g. 'C:\\...') or UNC path (e.g. '\\\\server\\share\\...'): '{expanded}'.";
            return false;
        }

        // Check for invalid path characters
        var invalidChars = Path.GetInvalidPathChars();
        if (expanded.IndexOfAny(invalidChars) >= 0)
        {
            errorMessage = $"Path contains invalid characters: '{expanded}'.";
            return false;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(expanded);
        }
        catch (Exception ex)
        {
            errorMessage = $"Invalid path format '{expanded}': {ex.Message}";
            return false;
        }

        // Re-verify resolved full path is still drive or UNC
        if (!IsRootedDriveOrUnc(fullPath))
        {
            errorMessage = $"Resolved path must be a fully rooted drive or UNC path: '{fullPath}'.";
            return false;
        }

        normalizedPath = CanonicalizeSeparators(fullPath);
        return true;
    }

    /// <summary>
    /// Checks whether a path string is a fully rooted Windows drive letter (e.g. C:\) or UNC path (\\server\share\...).
    /// Rejects device namespace aliases (\\?\, \\.\) and malformed UNCs without server or share.
    /// </summary>
    internal static bool IsRootedDriveOrUnc(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var trimmed = path.Trim();

        // Reject device namespace aliases
        if (trimmed.StartsWith(@"\\?\", StringComparison.Ordinal) ||
            trimmed.StartsWith(@"\\.\", StringComparison.Ordinal) ||
            trimmed.StartsWith("//?/", StringComparison.Ordinal) ||
            trimmed.StartsWith("//./", StringComparison.Ordinal))
        {
            return false;
        }

        // Windows drive: 'C:\...' or 'C:/...'
        if (trimmed.Length >= 3 &&
            char.IsAsciiLetter(trimmed[0]) &&
            trimmed[1] == ':' &&
            (trimmed[2] == '\\' || trimmed[2] == '/'))
        {
            return true;
        }

        // UNC: '\\server\share...' or '//server/share...'
        if (trimmed.Length >= 5 &&
            (trimmed.StartsWith(@"\\", StringComparison.Ordinal) || trimmed.StartsWith("//", StringComparison.Ordinal)))
        {
            var withoutPrefix = trimmed[2..];
            var parts = withoutPrefix.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && !string.IsNullOrWhiteSpace(parts[0]) && !string.IsNullOrWhiteSpace(parts[1]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Canonicalizes separators to '\' and trims trailing separator unless it is a drive or UNC root.
    /// </summary>
    internal static string CanonicalizeSeparators(string path)
    {
        var normalized = path.Replace('/', '\\');
        var root = Path.GetPathRoot(normalized);

        if (!string.IsNullOrEmpty(root))
        {
            var trimmedRoot = root.TrimEnd('\\');
            if (normalized.Equals(trimmedRoot, StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals(root, StringComparison.OrdinalIgnoreCase))
            {
                return EnsureTrailingSeparator(trimmedRoot);
            }
        }

        return normalized.TrimEnd('\\');
    }

    private static string EnsureTrailingSeparator(string path)
    {
        if (!path.EndsWith('\\'))
        {
            return path + '\\';
        }
        return path;
    }

    /// <summary>
    /// Determines whether two normalized paths are identical (case-insensitive).
    /// </summary>
    internal static bool ArePathsEqual(string pathA, string pathB)
    {
        return string.Equals(pathA, pathB, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines whether one directory path strictly contains another path (case-insensitive).
    /// Sibling names with common prefixes are not contained.
    /// </summary>
    internal static bool IsStrictDescendant(string ancestorPath, string descendantPath)
    {
        return descendantPath.Length > ancestorPath.Length
            && descendantPath.StartsWith(ancestorPath, StringComparison.OrdinalIgnoreCase)
            && (ancestorPath.EndsWith('\\') || descendantPath[ancestorPath.Length] == '\\');
    }

    /// <summary>
    /// Determines whether pathA and pathB have a containment relationship or are identical.
    /// </summary>
    internal static bool HasContainmentOrEqual(string pathA, string pathB)
    {
        return ArePathsEqual(pathA, pathB) || IsStrictDescendant(pathA, pathB) || IsStrictDescendant(pathB, pathA);
    }

    /// <summary>
    /// Inspects existing path ancestors and the target itself to ensure none are navigation links (symbolic link, junction, mount point).
    /// Inspects LinkTarget regardless of target existence so dangling links are also rejected.
    /// I/O and offline checking errors do not cause structural failure unless a link is known.
    /// </summary>
    internal static bool ValidateNoNavigationLinks(string normalizedPath, out string? invalidLinkPath, out string? errorMessage)
    {
        invalidLinkPath = null;
        errorMessage = null;

        // Check target itself (file or directory link)
        try
        {
            var fi = new FileInfo(normalizedPath);
            if (fi.LinkTarget is not null)
            {
                invalidLinkPath = normalizedPath;
                errorMessage = $"Path '{normalizedPath}' is a navigation link.";
                return false;
            }
        }
        catch
        {
            // Availability issue, not structural link detection
        }

        try
        {
            var di = new DirectoryInfo(normalizedPath);
            if (di.LinkTarget is not null)
            {
                invalidLinkPath = normalizedPath;
                errorMessage = $"Path '{normalizedPath}' is a navigation link.";
                return false;
            }
        }
        catch
        {
            // Availability issue, not structural link detection
        }

        // Walk ancestors up to root
        var parent = Path.GetDirectoryName(normalizedPath);
        while (!string.IsNullOrEmpty(parent))
        {
            try
            {
                var dirInfo = new DirectoryInfo(parent);
                if (dirInfo.LinkTarget is not null)
                {
                    invalidLinkPath = parent;
                    errorMessage = $"Path '{normalizedPath}' contains navigation link ancestor '{parent}'.";
                    return false;
                }
            }
            catch
            {
                // Availability issue, not structural link detection
            }

            var root = Path.GetPathRoot(parent);
            if (string.Equals(parent, root, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            parent = Path.GetDirectoryName(parent);
        }

        return true;
    }

    /// <summary>
    /// Checks whether a normalized path is currently accessible on disk.
    /// Missing sources or temporarily unavailable drives/shares return false with a warning message.
    /// </summary>
    internal static bool CheckAvailability(string normalizedPath, out string? warningMessage)
    {
        warningMessage = null;

        try
        {
            var root = Path.GetPathRoot(normalizedPath);
            if (!string.IsNullOrEmpty(root) && root.Length >= 2 && root[1] == ':')
            {
                var driveInfo = new DriveInfo(root[..1]);
                if (!driveInfo.IsReady)
                {
                    warningMessage = $"Drive '{root[..2]}' for path '{normalizedPath}' is not ready or offline.";
                    return false;
                }
            }

            if (File.Exists(normalizedPath) || Directory.Exists(normalizedPath))
            {
                return true;
            }

            var dir = Path.GetDirectoryName(normalizedPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                warningMessage = $"Path '{normalizedPath}' is not currently accessible.";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            warningMessage = $"Path '{normalizedPath}' is currently unavailable: {ex.Message}";
            return false;
        }
    }
}
