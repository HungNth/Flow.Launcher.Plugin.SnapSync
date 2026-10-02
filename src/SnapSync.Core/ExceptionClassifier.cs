using System;
using System.IO;

namespace SnapSync.Core;

/// <summary>
/// Categorizes exceptions into standardized failure categories.
/// Distinguishes sharing violations (locked files) from genuine permission denied using Win32 error codes and target lock detection.
/// </summary>
internal static class ExceptionClassifier
{
    private const int ErrorSharingViolation = 32;       // 0x20: ERROR_SHARING_VIOLATION
    private const int ErrorLockViolation = 33;          // 0x21: ERROR_LOCK_VIOLATION
    private const int ErrorAccessDenied = 5;            // 0x05: ERROR_ACCESS_DENIED
    private const int ErrorDiskFull = 112;              // 0x70: ERROR_DISK_FULL

    public static FailureCategory Classify(Exception ex, string? targetPath = null)
    {
        if (ex is null) return FailureCategory.None;

        var hResult = ex.HResult;
        var win32ErrorCode = hResult & 0xFFFF;
        var msg = ex.Message ?? string.Empty;

        // 1. Direct Win32 Sharing / Lock violation codes or explicit lock messages
        if (win32ErrorCode == ErrorSharingViolation ||
            win32ErrorCode == ErrorLockViolation ||
            msg.Contains("being used by another process", StringComparison.OrdinalIgnoreCase) ||
            msg.Contains("cannot access the file because it is being used", StringComparison.OrdinalIgnoreCase) ||
            msg.Contains("is locked", StringComparison.OrdinalIgnoreCase) ||
            msg.Contains("locked file", StringComparison.OrdinalIgnoreCase))
        {
            return FailureCategory.LockedFile;
        }

        // 2. Windows File.Move throws UnauthorizedAccessException when the target file exists and is open exclusively by another process.
        // If targetPath is provided (or extracted), verify whether opening the file triggers a sharing violation.
        if (ex is UnauthorizedAccessException)
        {
            if (!string.IsNullOrEmpty(targetPath) && File.Exists(targetPath))
            {
                if (IsFileLocked(targetPath))
                {
                    return FailureCategory.LockedFile;
                }
            }

            return FailureCategory.PermissionDenied;
        }

        if (ex is FileNotFoundException || ex is DirectoryNotFoundException)
        {
            return FailureCategory.MissingSource;
        }

        if (win32ErrorCode == ErrorAccessDenied)
        {
            return FailureCategory.PermissionDenied;
        }

        if (ex is PathTooLongException || ex is NotSupportedException || ex is ArgumentException)
        {
            return FailureCategory.InvalidPath;
        }

        if (ex is IOException ioEx)
        {
            if (msg.Contains("mutated during transfer", StringComparison.OrdinalIgnoreCase))
            {
                return FailureCategory.SourceMutated;
            }

            if (win32ErrorCode == ErrorDiskFull ||
                msg.Contains("disk is full", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("not enough space", StringComparison.OrdinalIgnoreCase))
            {
                return FailureCategory.DiskFull;
            }

            return FailureCategory.IoError;
        }

        return FailureCategory.IoError;
    }

    private static bool IsFileLocked(string filePath)
    {
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
        catch
        {
            return false;
        }
    }
}
