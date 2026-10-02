using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SnapSync.Core;

/// <summary>
/// Implements the high-level synchronization, preview, and validation operations.
/// </summary>
public sealed class SyncEngine : ISyncEngine
{
    private static readonly TimeSpan TimestampTolerance = TimeSpan.FromSeconds(2);
    private const int MaxReportDetails = 500;

    // Internal testing seam for deterministic cancellation and mid-stream mutation testing
    internal static Func<string, Task>? CopyProgressHook { get; set; }

    /// <inheritdoc/>
    public IReadOnlyList<ValidationError> Validate(SyncProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var (errors, plans) = PlanAndValidateProfile(profile);

        if (profile.Enabled)
        {
            var enabledPlans = plans.FindAll(p => p.Item.Enabled);
            CheckCrossItemConflicts(enabledPlans, errors);
        }

        return errors;
    }

    /// <inheritdoc/>
    public IReadOnlyList<ValidationError> Validate(SnapSyncConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var errors = new List<ValidationError>();
        if (configuration.Profiles == null || configuration.Profiles.Count == 0)
        {
            return errors;
        }

        var profileNameSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var allEnabledParticipants = new List<NormalizedItemPlan>();

        foreach (var profile in configuration.Profiles)
        {
            var (profileErrors, plans) = PlanAndValidateProfile(profile);
            errors.AddRange(profileErrors);

            if (!string.IsNullOrWhiteSpace(profile.Name))
            {
                var trimmedName = profile.Name.Trim();
                if (!profileNameSet.Add(trimmedName))
                {
                    errors.Add(new ValidationError(nameof(profile.Name), $"Profile name '{trimmedName}' must be unique across the configuration."));
                }
            }

            if (profile.Enabled)
            {
                foreach (var plan in plans)
                {
                    if (plan.Item.Enabled)
                    {
                        allEnabledParticipants.Add(plan);
                    }
                }
            }
        }

        CheckCrossItemConflicts(allEnabledParticipants, errors);
        return errors;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetAvailabilityWarnings(SyncProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var warnings = new List<string>();

        if (profile.Items == null || profile.Items.Count == 0)
        {
            return warnings;
        }

        foreach (var item in profile.Items)
        {
            if (!item.Enabled)
            {
                continue;
            }

            if (PathSafety.TryNormalizePath(item.Source, out var normalizedSource, out _) && normalizedSource is not null)
            {
                if (!File.Exists(normalizedSource) && !Directory.Exists(normalizedSource))
                {
                    warnings.Add($"Source path '{normalizedSource}' does not currently exist or is offline.");
                }
            }

            if (item.Destinations != null)
            {
                foreach (var destination in item.Destinations)
                {
                    if (string.IsNullOrWhiteSpace(destination))
                    {
                        continue;
                    }

                    if (PathSafety.TryNormalizePath(destination, out var normalizedDest, out _) && normalizedDest is not null)
                    {
                        if (!PathSafety.CheckAvailability(normalizedDest, out var warn) && warn is not null)
                        {
                            warnings.Add(warn);
                        }
                    }
                }
            }
        }

        return warnings;
    }

    /// <inheritdoc/>
    public async Task<SyncReport> SynchronizeAllAsync(SnapSyncConfiguration configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (configuration.Profiles == null || configuration.Profiles.Count == 0)
        {
            return new SyncReport(0, 0, 0, []);
        }

        var validationErrors = Validate(configuration);
        if (validationErrors.Count > 0)
        {
            var failedEntries = new List<EntryResult>();
            foreach (var err in validationErrors)
            {
                failedEntries.Add(new EntryResult(
                    Source: "Catalog",
                    Destination: null,
                    Outcome: EntryOutcome.Failed,
                    ErrorMessage: $"Validation error on {err.PropertyName}: {err.Message}",
                    Category: FailureCategory.ValidationError));
            }
            return new SyncReport(0, 0, failedEntries.Count, failedEntries);
        }

        var allEntries = new List<EntryResult>();
        var totalCopied = 0;
        var totalUnchanged = 0;
        var totalFailed = 0;

        foreach (var profile in configuration.Profiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!profile.Enabled)
            {
                continue;
            }

            var report = await ExecuteOperationAsync(profile, isPreview: false, cancellationToken).ConfigureAwait(false);
            totalCopied += report.CopiedCount;
            totalUnchanged += report.UnchangedCount;
            totalFailed += report.FailedCount;
            allEntries.AddRange(report.Entries);
        }

        var prioritized = PrioritizeReportEntries(allEntries, out var isTruncated);
        return new SyncReport(totalCopied, totalUnchanged, totalFailed, prioritized, isTruncated, allEntries.Count);
    }

    /// <inheritdoc/>
    public Task<SyncReport> SynchronizeAsync(SyncProfile profile, CancellationToken cancellationToken = default)
    {
        return ExecuteOperationAsync(profile, isPreview: false, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<SyncReport> PreviewAsync(SyncProfile profile, CancellationToken cancellationToken = default)
    {
        return ExecuteOperationAsync(profile, isPreview: true, cancellationToken);
    }

    private static async Task<SyncReport> ExecuteOperationAsync(
        SyncProfile profile,
        bool isPreview,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (!profile.Enabled)
        {
            return new SyncReport(0, 0, 0, []);
        }

        var (errors, plans) = PlanAndValidateProfile(profile);
        var enabledPlans = plans.FindAll(p => p.Item.Enabled);
        CheckCrossItemConflicts(enabledPlans, errors);

        if (errors.Count > 0)
        {
            var failedEntries = new List<EntryResult>();
            foreach (var err in errors)
            {
                failedEntries.Add(new EntryResult(
                    Source: profile.Name,
                    Destination: null,
                    Outcome: EntryOutcome.Failed,
                    ErrorMessage: $"Validation error on {err.PropertyName}: {err.Message}",
                    Category: FailureCategory.ValidationError));
            }

            return new SyncReport(0, 0, failedEntries.Count, failedEntries);
        }

        var rawEntries = new List<EntryResult>();
        var operationSourceHashCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var copiedCount = 0;
        var unchangedCount = 0;
        var failedCount = 0;

        var wasCancelled = false;
        try
        {
            foreach (var plan in enabledPlans)
            {
                cancellationToken.ThrowIfCancellationRequested();

            var sourcePath = plan.NormalizedSource;
            var isFile = File.Exists(sourcePath);
            var isDir = Directory.Exists(sourcePath);

            var effectiveType = plan.Item.ItemType;
            if (effectiveType == SyncItemType.Auto)
            {
                if (isFile) effectiveType = SyncItemType.File;
                else if (isDir) effectiveType = SyncItemType.Directory;
                else
                {
                    failedCount++;
                    rawEntries.Add(new EntryResult(
                        Source: sourcePath,
                        Destination: null,
                        Outcome: EntryOutcome.MissingSource,
                        ErrorMessage: $"Source path does not exist (Auto mode): '{sourcePath}'.",
                        Category: FailureCategory.MissingSource));
                    continue;
                }
            }

            if (effectiveType == SyncItemType.File)
            {
                if (!isFile)
                {
                    failedCount++;
                    if (isDir)
                    {
                        rawEntries.Add(new EntryResult(
                            Source: sourcePath,
                            Destination: null,
                            Outcome: EntryOutcome.Failed,
                            ErrorMessage: $"Source '{sourcePath}' is a directory, but item type is configured as File.",
                            Category: FailureCategory.ValidationError));
                    }
                    else
                    {
                        rawEntries.Add(new EntryResult(
                            Source: sourcePath,
                            Destination: null,
                            Outcome: EntryOutcome.MissingSource,
                            ErrorMessage: $"Source file does not exist: '{sourcePath}'.",
                            Category: FailureCategory.MissingSource));
                    }
                    continue;
                }

                var sourceInfo = new FileInfo(sourcePath);

                foreach (var destPath in plan.NormalizedDestinations)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var destInfo = new FileInfo(destPath);
                    var isUnchanged = false;

                    if (destInfo.Exists)
                    {
                        if (plan.Item.ComparisonMode == ComparisonMode.Sha256)
                        {
                            try
                            {
                                if (!operationSourceHashCache.TryGetValue(sourcePath, out var cachedHash))
                                {
                                    cachedHash = await ContentHasher.ComputeSha256Async(sourcePath, cancellationToken).ConfigureAwait(false);
                                    operationSourceHashCache[sourcePath] = cachedHash;
                                }
                                var destHash = await ContentHasher.ComputeSha256Async(destPath, cancellationToken).ConfigureAwait(false);
                                isUnchanged = string.Equals(cachedHash, destHash, StringComparison.OrdinalIgnoreCase);
                            }
                            catch (OperationCanceledException)
                            {
                                throw;
                            }
                            catch (Exception ex)
                            {
                                failedCount++;
                                rawEntries.Add(new EntryResult(
                                    Source: sourcePath,
                                    Destination: destPath,
                                    Outcome: EntryOutcome.Failed,
                                    ErrorMessage: $"Hash comparison failure: {ex.Message}",
                                    Category: FailureCategory.HashFailure));
                                continue;
                            }
                        }
                        else
                        {
                            isUnchanged = IsUnchangedFast(sourceInfo, destInfo);
                        }
                    }

                    if (isUnchanged)
                    {
                        unchangedCount++;
                        rawEntries.Add(new EntryResult(
                            Source: sourcePath,
                            Destination: destPath,
                            Outcome: EntryOutcome.Unchanged));
                        continue;
                    }

                    if (isPreview)
                    {
                        copiedCount++;
                        rawEntries.Add(new EntryResult(
                            Source: sourcePath,
                            Destination: destPath,
                            Outcome: EntryOutcome.WouldCopy));
                    }
                    else
                    {
                        try
                        {
                            await CopyFileFailSafeAsync(sourceInfo, destPath, cancellationToken).ConfigureAwait(false);
                            copiedCount++;
                            rawEntries.Add(new EntryResult(
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
                            rawEntries.Add(new EntryResult(
                                Source: sourcePath,
                                Destination: destPath,
                                Outcome: EntryOutcome.Failed,
                                ErrorMessage: ex.Message,
                                Category: ExceptionClassifier.Classify(ex, destPath)));
                        }
                    }
                }
            }
            else // Directory mode
            {
                if (!isDir)
                {
                    failedCount++;
                    if (isFile)
                    {
                        rawEntries.Add(new EntryResult(
                            Source: sourcePath,
                            Destination: null,
                            Outcome: EntryOutcome.Failed,
                            ErrorMessage: $"Source '{sourcePath}' is a file, but item type is configured as Directory.",
                            Category: FailureCategory.ValidationError));
                    }
                    else
                    {
                        rawEntries.Add(new EntryResult(
                            Source: sourcePath,
                            Destination: null,
                            Outcome: EntryOutcome.MissingSource,
                            ErrorMessage: $"Source directory does not exist: '{sourcePath}'.",
                            Category: FailureCategory.MissingSource));
                    }
                    continue;
                }

                var exclusionMatcher = new ExclusionMatcher(plan.Item.Exclusions);
                foreach (var destPath in plan.NormalizedDestinations)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var (dirCopied, dirUnchanged, dirFailed) = await SynchronizeDirectoryInternalAsync(
                            sourcePath,
                            destPath,
                            exclusionMatcher,
                            plan.Item.ComparisonMode,
                            operationSourceHashCache,
                            rawEntries,
                            isPreview,
                            cancellationToken).ConfigureAwait(false);

                        copiedCount += dirCopied;
                        unchangedCount += dirUnchanged;
                        failedCount += dirFailed;
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        failedCount++;
                        rawEntries.Add(new EntryResult(
                            Source: sourcePath,
                            Destination: destPath,
                            Outcome: EntryOutcome.Failed,
                            ErrorMessage: ex.Message,
                            Category: ExceptionClassifier.Classify(ex)));
                    }
                }
            }
            }
        }
        catch (OperationCanceledException)
        {
            wasCancelled = true;
        }

        var prioritizedEntries = PrioritizeReportEntries(rawEntries, out var isTruncated);
        return new SyncReport(copiedCount, unchangedCount, failedCount, prioritizedEntries, isTruncated, rawEntries.Count, wasCancelled);
    }

    private static IReadOnlyList<EntryResult> PrioritizeReportEntries(List<EntryResult> allEntries, out bool isTruncated)
    {
        if (allEntries.Count <= MaxReportDetails)
        {
            isTruncated = false;
            return allEntries;
        }

        isTruncated = true;
        return allEntries
            .OrderBy(e => GetPriority(e.Outcome))
            .Take(MaxReportDetails)
            .ToList();
    }

    private static int GetPriority(EntryOutcome outcome)
    {
        return outcome switch
        {
            EntryOutcome.Failed => 0,
            EntryOutcome.MissingSource => 1,
            EntryOutcome.Copied => 2,
            EntryOutcome.WouldCopy => 3,
            EntryOutcome.CreatedDirectory => 4,
            EntryOutcome.WouldCreateDirectory => 5,
            EntryOutcome.Skipped => 6,
            EntryOutcome.Excluded => 7,
            EntryOutcome.Unchanged => 8,
            _ => 9
        };
    }

    private static async Task<(int Copied, int Unchanged, int Failed)> SynchronizeDirectoryInternalAsync(
        string sourceRoot,
        string destRoot,
        ExclusionMatcher exclusionMatcher,
        ComparisonMode comparisonMode,
        Dictionary<string, string> sourceHashCache,
        List<EntryResult> entries,
        bool isPreview,
        CancellationToken cancellationToken)
    {
        var copied = 0;
        var unchanged = 0;
        var failed = 0;

        if (!Directory.Exists(destRoot))
        {
            if (isPreview)
            {
                entries.Add(new EntryResult(
                    Source: sourceRoot,
                    Destination: destRoot,
                    Outcome: EntryOutcome.WouldCreateDirectory));
            }
            else
            {
                Directory.CreateDirectory(destRoot);
                entries.Add(new EntryResult(
                    Source: sourceRoot,
                    Destination: destRoot,
                    Outcome: EntryOutcome.CreatedDirectory));
            }
        }

        var dirQueue = new Queue<string>();
        dirQueue.Enqueue(sourceRoot);

        while (dirQueue.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var currentSourceDir = dirQueue.Dequeue();

            var relativeDir = Path.GetRelativePath(sourceRoot, currentSourceDir);
            if (relativeDir == ".") relativeDir = string.Empty;

            var currentDestDir = string.IsNullOrEmpty(relativeDir)
                ? destRoot
                : Path.Combine(destRoot, relativeDir);

            if (!Directory.Exists(currentDestDir))
            {
                if (isPreview)
                {
                    entries.Add(new EntryResult(
                        Source: currentSourceDir,
                        Destination: currentDestDir,
                        Outcome: EntryOutcome.WouldCreateDirectory));
                }
                else
                {
                    Directory.CreateDirectory(currentDestDir);
                    entries.Add(new EntryResult(
                        Source: currentSourceDir,
                        Destination: currentDestDir,
                        Outcome: EntryOutcome.CreatedDirectory));
                }
            }

            var dirInfo = new DirectoryInfo(currentSourceDir);

            DirectoryInfo[] subDirs;
            try
            {
                subDirs = dirInfo.GetDirectories();
            }
            catch (Exception ex)
            {
                failed++;
                entries.Add(new EntryResult(
                    Source: currentSourceDir,
                    Destination: currentDestDir,
                    Outcome: EntryOutcome.Failed,
                    ErrorMessage: $"Failed to enumerate directories in '{currentSourceDir}': {ex.Message}",
                    Category: ExceptionClassifier.Classify(ex)));
                continue;
            }

            foreach (var subDir in subDirs)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var relSubDir = Path.GetRelativePath(sourceRoot, subDir.FullName).Replace('\\', '/');

                if (subDir.LinkTarget is not null)
                {
                    entries.Add(new EntryResult(
                        Source: subDir.FullName,
                        Destination: Path.Combine(destRoot, relSubDir.Replace('/', '\\')),
                        Outcome: EntryOutcome.Skipped,
                        ErrorMessage: "Navigation link skipped."));
                    continue;
                }

                if (exclusionMatcher.IsExcluded(relSubDir, isDirectory: true))
                {
                    entries.Add(new EntryResult(
                        Source: subDir.FullName,
                        Destination: null,
                        Outcome: EntryOutcome.Excluded));
                    continue;
                }

                dirQueue.Enqueue(subDir.FullName);
            }

            FileInfo[] files;
            try
            {
                files = dirInfo.GetFiles();
            }
            catch (Exception ex)
            {
                failed++;
                entries.Add(new EntryResult(
                    Source: currentSourceDir,
                    Destination: currentDestDir,
                    Outcome: EntryOutcome.Failed,
                    ErrorMessage: $"Failed to enumerate files in '{currentSourceDir}': {ex.Message}",
                    Category: ExceptionClassifier.Classify(ex)));
                continue;
            }

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var relFile = Path.GetRelativePath(sourceRoot, file.FullName).Replace('\\', '/');

                if (file.LinkTarget is not null)
                {
                    entries.Add(new EntryResult(
                        Source: file.FullName,
                        Destination: Path.Combine(destRoot, relFile.Replace('/', '\\')),
                        Outcome: EntryOutcome.Skipped,
                        ErrorMessage: "Navigation link skipped."));
                    continue;
                }

                if (exclusionMatcher.IsExcluded(relFile))
                {
                    entries.Add(new EntryResult(
                        Source: file.FullName,
                        Destination: null,
                        Outcome: EntryOutcome.Excluded));
                    continue;
                }

                var targetFilePath = Path.Combine(destRoot, relFile.Replace('/', '\\'));
                var destFileInfo = new FileInfo(targetFilePath);
                var isUnchanged = false;

                if (destFileInfo.Exists)
                {
                    if (comparisonMode == ComparisonMode.Sha256)
                    {
                        try
                        {
                            if (!sourceHashCache.TryGetValue(file.FullName, out var srcHash))
                            {
                                srcHash = await ContentHasher.ComputeSha256Async(file.FullName, cancellationToken).ConfigureAwait(false);
                                sourceHashCache[file.FullName] = srcHash;
                            }
                            var dstHash = await ContentHasher.ComputeSha256Async(targetFilePath, cancellationToken).ConfigureAwait(false);
                            isUnchanged = string.Equals(srcHash, dstHash, StringComparison.OrdinalIgnoreCase);
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            failed++;
                            entries.Add(new EntryResult(
                                Source: file.FullName,
                                Destination: targetFilePath,
                                Outcome: EntryOutcome.Failed,
                                ErrorMessage: $"Hash comparison failure: {ex.Message}",
                                Category: FailureCategory.HashFailure));
                            continue;
                        }
                    }
                    else
                    {
                        isUnchanged = IsUnchangedFast(file, destFileInfo);
                    }
                }

                if (isUnchanged)
                {
                    unchanged++;
                    entries.Add(new EntryResult(
                        Source: file.FullName,
                        Destination: targetFilePath,
                        Outcome: EntryOutcome.Unchanged));
                    continue;
                }

                if (isPreview)
                {
                    copied++;
                    entries.Add(new EntryResult(
                        Source: file.FullName,
                        Destination: targetFilePath,
                        Outcome: EntryOutcome.WouldCopy));
                }
                else
                {
                    try
                    {
                        await CopyFileFailSafeAsync(file, targetFilePath, cancellationToken).ConfigureAwait(false);
                        copied++;
                        entries.Add(new EntryResult(
                            Source: file.FullName,
                            Destination: targetFilePath,
                            Outcome: EntryOutcome.Copied));
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        entries.Add(new EntryResult(
                            Source: file.FullName,
                            Destination: targetFilePath,
                            Outcome: EntryOutcome.Failed,
                            ErrorMessage: ex.Message,
                            Category: ExceptionClassifier.Classify(ex, targetFilePath)));
                    }
                }
            }
        }

        return (copied, unchanged, failed);
    }

    private static (List<ValidationError> Errors, List<NormalizedItemPlan> Plans) PlanAndValidateProfile(SyncProfile profile)
    {
        var errors = new List<ValidationError>();
        var plans = new List<NormalizedItemPlan>();

        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            errors.Add(new ValidationError(nameof(profile.Name), "Profile name cannot be empty."));
        }
        else
        {
            var trimmed = profile.Name.Trim();
            if (string.Equals(trimmed, "all", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(new ValidationError(nameof(profile.Name), "Profile name 'all' is reserved."));
            }
            else if (trimmed.StartsWith(':'))
            {
                errors.Add(new ValidationError(nameof(profile.Name), "Profile name cannot begin with ':'."));
            }
        }

        if (profile.Items == null || profile.Items.Count == 0)
        {
            errors.Add(new ValidationError(nameof(profile.Items), "Profile must contain at least one synchronization item."));
            return (errors, plans);
        }

        var itemNameSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < profile.Items.Count; i++)
        {
            var item = profile.Items[i];
            if (string.IsNullOrWhiteSpace(item.Name))
            {
                errors.Add(new ValidationError(nameof(item.Name), $"Item at index {i} must have a name."));
            }
            else
            {
                var trimmedItemName = item.Name.Trim();
                if (!itemNameSet.Add(trimmedItemName))
                {
                    errors.Add(new ValidationError(nameof(item.Name), $"Item name '{trimmedItemName}' must be unique within profile '{profile.Name}'."));
                }
            }

            if (ValidateSingleItem(item, i, errors, out var plan) && plan is not null)
            {
                plans.Add(plan);
            }
        }

        return (errors, plans);
    }

    private static bool ValidateSingleItem(
        SyncItem item,
        int itemIndex,
        List<ValidationError> errors,
        out NormalizedItemPlan? plan)
    {
        plan = null;
        var initialErrorCount = errors.Count;

        string? normalizedSource = null;
        if (string.IsNullOrWhiteSpace(item.Source))
        {
            errors.Add(new ValidationError(nameof(item.Source), $"Item at index {itemIndex} must have a valid Source path."));
        }
        else if (!PathSafety.TryNormalizePath(item.Source, out normalizedSource, out var sourceErr) || normalizedSource is null)
        {
            errors.Add(new ValidationError(nameof(item.Source), $"Item '{item.Name}' Source '{item.Source}' is invalid: {sourceErr}"));
        }
        else if (!PathSafety.ValidateNoNavigationLinks(normalizedSource, out _, out var linkErr))
        {
            errors.Add(new ValidationError(nameof(item.Source), $"Item '{item.Name}' Source '{item.Source}': {linkErr}"));
        }

        if (item.Destinations == null || item.Destinations.Count == 0 || item.Destinations.TrueForAll(string.IsNullOrWhiteSpace))
        {
            errors.Add(new ValidationError(nameof(item.Destinations), $"Item at index {itemIndex} must have at least one valid Destination path."));
            return false;
        }

        var normalizedDestinations = new List<string>();
        var destinationSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var d = 0; d < item.Destinations.Count; d++)
        {
            var rawDest = item.Destinations[d];
            if (string.IsNullOrWhiteSpace(rawDest))
            {
                errors.Add(new ValidationError(nameof(item.Destinations), $"Item '{item.Name}' destination at index {d} cannot be empty."));
                continue;
            }

            if (!PathSafety.TryNormalizePath(rawDest, out var normDest, out var destErr) || normDest is null)
            {
                errors.Add(new ValidationError(nameof(item.Destinations), $"Item '{item.Name}' destination '{rawDest}' is invalid: {destErr}"));
                continue;
            }

            if (!destinationSet.Add(normDest))
            {
                errors.Add(new ValidationError(nameof(item.Destinations), $"Item '{item.Name}' contains duplicate destination '{normDest}'."));
                continue;
            }

            if (!PathSafety.ValidateNoNavigationLinks(normDest, out _, out var destLinkErr))
            {
                errors.Add(new ValidationError(nameof(item.Destinations), $"Item '{item.Name}' destination '{rawDest}': {destLinkErr}"));
                continue;
            }

            if (normalizedSource is not null && PathSafety.HasContainmentOrEqual(normalizedSource, normDest))
            {
                errors.Add(new ValidationError(
                    nameof(item.Destinations),
                    $"Item '{item.Name}' contains identical or nested Source/Destination relationship: '{normalizedSource}' and '{normDest}'."));
            }

            normalizedDestinations.Add(normDest);
        }

        for (var j = 0; j < normalizedDestinations.Count; j++)
        {
            for (var k = j + 1; k < normalizedDestinations.Count; k++)
            {
                var d1 = normalizedDestinations[j];
                var d2 = normalizedDestinations[k];
                if (PathSafety.HasContainmentOrEqual(d1, d2))
                {
                    errors.Add(new ValidationError(
                        nameof(item.Destinations),
                        $"Item '{item.Name}' contains overlapping destinations: '{d1}' and '{d2}'."));
                }
            }
        }

        if (errors.Count == initialErrorCount && normalizedSource is not null)
        {
            plan = new NormalizedItemPlan(item, normalizedSource, normalizedDestinations);
            return true;
        }

        return false;
    }

    private static void CheckCrossItemConflicts(List<NormalizedItemPlan> enabledPlans, List<ValidationError> errors)
    {
        for (var i = 0; i < enabledPlans.Count; i++)
        {
            var planA = enabledPlans[i];

            for (var j = i + 1; j < enabledPlans.Count; j++)
            {
                var planB = enabledPlans[j];

                foreach (var destA in planA.NormalizedDestinations)
                {
                    foreach (var destB in planB.NormalizedDestinations)
                    {
                        if (PathSafety.HasContainmentOrEqual(destA, destB))
                        {
                            errors.Add(new ValidationError(
                                nameof(SyncItem.Destinations),
                                $"Conflicting destination write areas between item '{planA.Item.Name}' ('{destA}') and item '{planB.Item.Name}' ('{destB}')."));
                        }
                    }

                    if (PathSafety.HasContainmentOrEqual(destA, planB.NormalizedSource))
                    {
                        errors.Add(new ValidationError(
                            nameof(SyncItem.Destinations),
                            $"Conflicting read/write overlap between destination '{destA}' of '{planA.Item.Name}' and source '{planB.NormalizedSource}' of '{planB.Item.Name}'."));
                    }
                }

                foreach (var destB in planB.NormalizedDestinations)
                {
                    if (PathSafety.HasContainmentOrEqual(destB, planA.NormalizedSource))
                    {
                        errors.Add(new ValidationError(
                            nameof(SyncItem.Destinations),
                            $"Conflicting read/write overlap between destination '{destB}' of '{planB.Item.Name}' and source '{planA.NormalizedSource}' of '{planA.Item.Name}'."));
                    }
                }
            }
        }
    }

    private static bool IsUnchangedFast(FileInfo source, FileInfo destination)
    {
        if (source.Length != destination.Length)
        {
            return false;
        }

        var sourceUtc = source.LastWriteTimeUtc;
        var destUtc = destination.LastWriteTimeUtc;
        var diff = sourceUtc > destUtc ? sourceUtc - destUtc : destUtc - sourceUtc;

        return diff <= TimestampTolerance;
    }

    private static async Task CopyFileFailSafeAsync(
        FileInfo sourceInfo,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        var destinationDirectory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(destinationDirectory) && !Directory.Exists(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        var tempFilePath = destinationPath + ".tmp." + Guid.NewGuid().ToString("N");
        var initialLength = sourceInfo.Length;
        var initialUtc = sourceInfo.LastWriteTimeUtc;

        try
        {
            const int bufferSize = 81920;
            await using (var sourceStream = new FileStream(sourceInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan))
            await using (var tempStream = new FileStream(tempFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await sourceStream.CopyToAsync(tempStream, bufferSize, cancellationToken).ConfigureAwait(false);
            }

            if (CopyProgressHook != null)
            {
                await CopyProgressHook(sourceInfo.FullName).ConfigureAwait(false);
            }

            // Pre-replacement source stability check
            sourceInfo.Refresh();
            if (sourceInfo.Length != initialLength || sourceInfo.LastWriteTimeUtc != initialUtc)
            {
                throw new IOException($"Source '{sourceInfo.FullName}' mutated during transfer. Discarding staged copy without retry.");
            }

            File.SetLastWriteTimeUtc(tempFilePath, initialUtc);
            File.Move(tempFilePath, destinationPath, overwrite: true);
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
                // Best-effort cleanup
            }

            throw;
        }
    }

    private sealed class NormalizedItemPlan
    {
        public SyncItem Item { get; }
        public string NormalizedSource { get; }
        public IReadOnlyList<string> NormalizedDestinations { get; }

        public NormalizedItemPlan(SyncItem item, string normalizedSource, IReadOnlyList<string> normalizedDestinations)
        {
            Item = item;
            NormalizedSource = normalizedSource;
            NormalizedDestinations = normalizedDestinations;
        }
    }
}
