using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SnapSync.Core;

/// <summary>
/// Provides core synchronization and validation operations.
/// </summary>
public sealed class SyncEngine : ISyncEngine
{
    private const double FastComparisonToleranceSeconds = 2.0;
    private const int StreamBufferSize = 81920;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncEngine"/> class.
    /// </summary>
    public SyncEngine()
    {
    }

    /// <inheritdoc/>
    public IReadOnlyList<ValidationError> Validate(SyncProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var errors = new List<ValidationError>();

        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            errors.Add(new ValidationError(nameof(profile.Name), "Profile name cannot be empty."));
        }

        if (profile.Items == null || profile.Items.Count == 0)
        {
            errors.Add(new ValidationError(nameof(profile.Items), "Profile must contain at least one synchronization item."));
            return errors;
        }

        for (var i = 0; i < profile.Items.Count; i++)
        {
            var item = profile.Items[i];
            if (string.IsNullOrWhiteSpace(item.Source))
            {
                errors.Add(new ValidationError(nameof(item.Source), $"Item at index {i} must have a valid Source path."));
            }

            if (item.Destinations == null || item.Destinations.Count == 0 || item.Destinations.TrueForAll(string.IsNullOrWhiteSpace))
            {
                errors.Add(new ValidationError(nameof(item.Destinations), $"Item at index {i} must have at least one valid Destination path."));
            }
        }

        return errors;
    }

    /// <inheritdoc/>
    public async Task<SyncReport> SynchronizeAsync(SyncProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (!profile.Enabled)
        {
            return new SyncReport(0, 0, 0, []);
        }

        var entries = new List<EntryResult>();
        var copiedCount = 0;
        var unchangedCount = 0;
        var failedCount = 0;

        foreach (var item in profile.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!item.Enabled)
            {
                continue;
            }

            var sourcePath = Path.GetFullPath(item.Source);
            var sourceInfo = new FileInfo(sourcePath);

            if (!sourceInfo.Exists)
            {
                failedCount++;
                entries.Add(new EntryResult(
                    Source: sourcePath,
                    Destination: null,
                    Outcome: EntryOutcome.Failed,
                    ErrorMessage: $"Source file does not exist: '{sourcePath}'."));
                continue;
            }

            foreach (var rawDestination in item.Destinations)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(rawDestination))
                {
                    continue;
                }

                var destPath = Path.GetFullPath(rawDestination);
                var destInfo = new FileInfo(destPath);

                if (destInfo.Exists && IsUnchangedFast(sourceInfo, destInfo))
                {
                    unchangedCount++;
                    entries.Add(new EntryResult(
                        Source: sourcePath,
                        Destination: destPath,
                        Outcome: EntryOutcome.Unchanged));
                    continue;
                }

                try
                {
                    await CopyFileFailSafeAsync(sourceInfo, destPath, cancellationToken).ConfigureAwait(false);
                    copiedCount++;
                    entries.Add(new EntryResult(
                        Source: sourcePath,
                        Destination: destPath,
                        Outcome: EntryOutcome.Copied));
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    failedCount++;
                    entries.Add(new EntryResult(
                        Source: sourcePath,
                        Destination: destPath,
                        Outcome: EntryOutcome.Failed,
                        ErrorMessage: ex.Message));
                }
            }
        }

        return new SyncReport(copiedCount, unchangedCount, failedCount, entries);
    }

    private static bool IsUnchangedFast(FileInfo source, FileInfo destination)
    {
        if (source.Length != destination.Length)
        {
            return false;
        }

        var differenceSeconds = Math.Abs((source.LastWriteTimeUtc - destination.LastWriteTimeUtc).TotalSeconds);
        return differenceSeconds <= FastComparisonToleranceSeconds;
    }

    private static async Task CopyFileFailSafeAsync(
        FileInfo sourceInfo,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        var destDirectory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(destDirectory))
        {
            Directory.CreateDirectory(destDirectory);
        }

        var destFileName = Path.GetFileName(destinationPath);
        var tempFileName = $".{destFileName}.tmp.{Guid.NewGuid():N}";
        var tempFilePath = Path.Combine(destDirectory ?? string.Empty, tempFileName);

        try
        {
            var sourceLastWriteUtc = sourceInfo.LastWriteTimeUtc;

            await using (var sourceStream = new FileStream(
                sourceInfo.FullName,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                StreamBufferSize,
                useAsync: true))
            await using (var tempStream = new FileStream(
                tempFilePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                StreamBufferSize,
                useAsync: true))
            {
                await sourceStream.CopyToAsync(tempStream, cancellationToken).ConfigureAwait(false);
                await tempStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(tempFilePath, destinationPath, overwrite: true);
            File.SetLastWriteTimeUtc(destinationPath, sourceLastWriteUtc);
        }
        catch
        {
            try
            {
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
            catch
            {
                // Best-effort cleanup of temporary file on failure.
            }

            throw;
        }
    }
}
