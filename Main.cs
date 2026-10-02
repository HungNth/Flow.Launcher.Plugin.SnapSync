using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.SnapSync.ViewModels;
using Flow.Launcher.Plugin.SnapSync.Views;
using SnapSync.Core;

namespace Flow.Launcher.Plugin.SnapSync;

/// <summary>
/// Main plugin entry point for Flow Launcher integration with single-flight concurrency and responsive cancellation.
/// </summary>
public class Main : IAsyncPlugin, ISettingProvider, IContextMenu
{
    private PluginInitContext _context = null!;
    private SnapSyncConfiguration _configuration = null!;
    private readonly ISyncEngine _syncEngine;

    // Single-flight active operation management
    private static readonly object _operationLock = new();
    private static CancellationTokenSource? _activeCts;
    private static string? _activeOperationDescription;

    // Retain only latest report in memory
    private static SyncReport? _latestReport;
    private static string _latestReportTitle = string.Empty;

    public Main() : this(new SyncEngine())
    {
    }

    public Main(ISyncEngine syncEngine)
    {
        _syncEngine = syncEngine ?? throw new ArgumentNullException(nameof(syncEngine));
    }

    private string? _configurationLoadError;

    /// <inheritdoc/>
    public Task InitAsync(PluginInitContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));

        try
        {
            _configuration = _context.API.LoadSettingJsonStorage<SnapSyncConfiguration>();
            if (_configuration == null)
            {
                _configurationLoadError = "Configuration loaded as null.";
                _configuration = new SnapSyncConfiguration { Profiles = [] };
            }
            else if (_configuration.SchemaVersion > 1)
            {
                _configurationLoadError = $"Unsupported configuration schema version: {_configuration.SchemaVersion}. Current supported version is 1.";
            }
            else if (_configuration.Profiles == null)
            {
                _configuration.Profiles = [];
            }
        }
        catch (Exception ex)
        {
            _configurationLoadError = $"Corrupt configuration file: {ex.Message}";
            _configuration = new SnapSyncConfiguration { Profiles = [] };
            _context.API.LogException(nameof(SnapSync), "Failed to load SnapSyncConfiguration JSON", ex);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<List<Result>> QueryAsync(Query query, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        var results = new List<Result>();
        var rawSearch = query.Search?.Trim() ?? string.Empty;


        // Handle configuration load error without overwriting corrupt file
        if (_configurationLoadError != null)
        {
            results.Add(new Result
            {
                Title = "SnapSync: Configuration Error",
                SubTitle = $"{_configurationLoadError} Open settings to repair; file was not overwritten.",
                IcoPath = "Images\\app.png",
                Score = 3000,
                Action = _ =>
                {
                    _context.API.OpenSettingDialog();
                    return true;
                }
            });
            return Task.FromResult(results);
        }
        // 1. If an operation is currently active, show busy status and Cancel option
        string? currentActiveDesc;
        lock (_operationLock)
        {
            currentActiveDesc = _activeOperationDescription;
        }

        if (currentActiveDesc != null)
        {
            results.Add(new Result
            {
                Title = $"SnapSync: Busy ({currentActiveDesc})",
                SubTitle = "An operation is currently in progress. Select below to cancel.",
                IcoPath = "Images\\app.png",
                Score = 2000
            });

            results.Add(new Result
            {
                Title = "Cancel current operation",
                SubTitle = $"Cancel '{currentActiveDesc}' and receive a partial report",
                IcoPath = "Images\\app.png",
                Score = 1900,
                Action = _ =>
                {
                    CancelActiveOperation();
                    return true;
                }
            });
        }

        // 2. Report mode: sy :report [filter]
        if (rawSearch.StartsWith(":report", StringComparison.OrdinalIgnoreCase))
        {
            var filter = rawSearch.Length > 7 ? rawSearch[7..].Trim() : string.Empty;
            return Task.FromResult(QueryReportMode(filter));
        }

        if (_configuration.Profiles.Count == 0)
        {
            results.Add(new Result
            {
                Title = "SnapSync: No profiles configured",
                SubTitle = "Open settings to configure a sync profile",
                IcoPath = "Images\\app.png",
                Action = _ =>
                {
                    _context.API.OpenSettingDialog();
                    return true;
                }
            });
            return Task.FromResult(results);
        }

        // 3. Sync All query
        var isAllQuery = string.Equals(rawSearch, "all", StringComparison.OrdinalIgnoreCase);
        if (isAllQuery)
        {
            var enabledCount = _configuration.Profiles.Count(p => p.Enabled);
            results.Add(new Result
            {
                Title = "SnapSync: Sync All",
                SubTitle = $"Synchronize all {enabledCount} enabled profile(s) sequentially",
                IcoPath = "Images\\app.png",
                Score = 1000,
                AsyncAction = async _ =>
                {
                    await TryRunSingleFlightAsync("Sync All", async ct =>
                    {
                        var snapshot = CloneConfiguration(_configuration);
                        var report = await _syncEngine.SynchronizeAllAsync(snapshot, ct).ConfigureAwait(false);
                        _latestReport = report;
                        _latestReportTitle = "Sync All";

                        var summary = $"Copied: {report.CopiedCount}, Unchanged: {report.UnchangedCount}, Failed: {report.FailedCount}";
                        var title = report.IsSuccess ? "SnapSync: Sync All Completed" : "SnapSync: Sync All Completed with errors";
                        _context.API.ShowMsg(title, summary);
                    });
                    return true;
                }
            });
        }

        // 4. Search profiles
        foreach (var profile in _configuration.Profiles)
        {
            token.ThrowIfCancellationRequested();

            var score = 100;
            if (!string.IsNullOrEmpty(rawSearch) && !isAllQuery)
            {
                var match = _context.API.FuzzySearch(rawSearch, profile.Name);
                if (!match.IsSearchPrecisionScoreMet())
                {
                    continue;
                }

                score = match.Score;
            }

            var itemCount = profile.Items?.Count ?? 0;
            var subTitle = !profile.Enabled
                ? $"[Disabled] {itemCount} sync item(s)"
                : string.IsNullOrWhiteSpace(profile.Description)
                    ? $"Enter: Sync | Ctrl+Enter: Preview ({itemCount} item(s))"
                    : $"{profile.Description} ({itemCount} item(s))";

            results.Add(new Result
            {
                Title = profile.Name,
                SubTitle = subTitle,
                IcoPath = "Images\\app.png",
                Score = score,
                ContextData = profile,
                Action = context =>
                {
                    if (context.SpecialKeyState.CtrlPressed)
                    {
                        _ = TryRunSingleFlightAsync($"Preview: {profile.Name}", async ct =>
                        {
                            var snapshot = CloneProfile(profile);
                            var report = await _syncEngine.PreviewAsync(snapshot, ct).ConfigureAwait(false);
                            _latestReport = report;
                            _latestReportTitle = $"Preview: {profile.Name}";

                            var summary = $"Would Copy: {report.CopiedCount}, Unchanged: {report.UnchangedCount}, Failed: {report.FailedCount}";
                            var title = report.IsSuccess
                                ? (report.CopiedCount == 0 ? $"SnapSync Preview: {profile.Name} (No changes)" : $"SnapSync Preview: {profile.Name} Completed")
                                : $"SnapSync Preview: {profile.Name} Completed with errors";
                            _context.API.ShowMsg(title, summary);
                        });
                        return true;
                    }

                    if (!profile.Enabled)
                    {
                        _context.API.OpenSettingDialog();
                        return true;
                    }

                    _ = TryRunSingleFlightAsync($"Sync: {profile.Name}", async ct =>
                    {
                        var snapshot = CloneProfile(profile);
                        var report = await _syncEngine.SynchronizeAsync(snapshot, ct).ConfigureAwait(false);
                        _latestReport = report;
                        _latestReportTitle = profile.Name;

                        var summary = $"Copied: {report.CopiedCount}, Unchanged: {report.UnchangedCount}, Failed: {report.FailedCount}";
                        var title = report.IsSuccess
                            ? (report.CopiedCount == 0 ? $"SnapSync: {profile.Name} (No changes)" : $"SnapSync: {profile.Name} Completed")
                            : $"SnapSync: {profile.Name} Completed with errors";
                        _context.API.ShowMsg(title, summary);
                    });
                    return true;
                }
            });
        }

        // 5. View last report action
        if (_latestReport != null && !isAllQuery)
        {
            results.Add(new Result
            {
                Title = $"SnapSync: View Last Report ({_latestReportTitle})",
                SubTitle = $"Copied/WouldCopy: {_latestReport.CopiedCount}, Unchanged: {_latestReport.UnchangedCount}, Failed: {_latestReport.FailedCount}. Press Enter to inspect entries",
                IcoPath = "Images\\app.png",
                Score = 50,
                Action = _ =>
                {
                    _context.API.ChangeQuery($"{query.ActionKeyword} :report ");
                    return false;
                }
            });
        }

        return Task.FromResult(results);
    }

    private List<Result> QueryReportMode(string filter)
    {
        var results = new List<Result>();

        if (_latestReport == null)
        {
            results.Add(new Result
            {
                Title = "SnapSync Report: No cached report available",
                SubTitle = "Run a Preview (Ctrl+Enter) or Synchronize (Enter) to generate a report",
                IcoPath = "Images\\app.png"
            });
            return results;
        }

        var truncationText = _latestReport.IsTruncated ? " [Truncated at 500 entries]" : string.Empty;
        results.Add(new Result
        {
            Title = $"Report Summary: {_latestReportTitle}{truncationText}",
            SubTitle = $"Copied/WouldCopy: {_latestReport.CopiedCount}, Unchanged: {_latestReport.UnchangedCount}, Failed: {_latestReport.FailedCount}",
            IcoPath = "Images\\app.png",
            Score = 1000
        });

        var filteredEntries = _latestReport.Entries;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            filteredEntries = _latestReport.Entries
                .Where(e => e.Source.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                            (e.Destination != null && e.Destination.Contains(filter, StringComparison.OrdinalIgnoreCase)) ||
                            e.Outcome.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        foreach (var entry in filteredEntries)
        {
            var destinationText = string.IsNullOrEmpty(entry.Destination) ? string.Empty : $" -> {entry.Destination}";
            var errorText = string.IsNullOrEmpty(entry.ErrorMessage) ? string.Empty : $" | Error: {entry.ErrorMessage}";
            var subTitle = $"[{entry.Outcome}]{destinationText}{errorText}";

            results.Add(new Result
            {
                Title = entry.Source,
                SubTitle = subTitle,
                IcoPath = "Images\\app.png",
                Score = 100,
                Action = _ =>
                {
                    var pathToCopy = !string.IsNullOrEmpty(entry.Destination) ? entry.Destination : entry.Source;
                    if (!string.IsNullOrEmpty(pathToCopy))
                    {
                        _context.API.CopyToClipboard(pathToCopy);
                        _context.API.ShowMsg("SnapSync", $"Copied path to clipboard: {pathToCopy}");
                    }
                    return true;
                }
            });
        }

        return results;
    }

    /// <inheritdoc/>
    public List<Result> LoadContextMenus(Result selected)
    {
        var results = new List<Result>();
        var profile = selected.ContextData as SyncProfile;
        if (profile == null)
        {
            return results;
        }

        if (profile.Enabled)
        {
            results.Add(new Result
            {
                Title = "Sync now",
                SubTitle = $"Synchronize '{profile.Name}' immediately",
                IcoPath = "Images\\app.png",
                AsyncAction = async _ =>
                {
                    await TryRunSingleFlightAsync($"Sync: {profile.Name}", async ct =>
                    {
                        var snapshot = CloneProfile(profile);
                        var report = await _syncEngine.SynchronizeAsync(snapshot, ct).ConfigureAwait(false);
                        _latestReport = report;
                        _latestReportTitle = profile.Name;

                        var summary = $"Copied: {report.CopiedCount}, Unchanged: {report.UnchangedCount}, Failed: {report.FailedCount}";
                        var title = report.IsSuccess ? $"SnapSync: {profile.Name} Completed" : $"SnapSync: {profile.Name} Completed with errors";
                        _context.API.ShowMsg(title, summary);
                    });
                    return true;
                }
            });

            results.Add(new Result
            {
                Title = "Preview changes",
                SubTitle = $"Inspect what would change in '{profile.Name}' without modifying files",
                IcoPath = "Images\\app.png",
                AsyncAction = async _ =>
                {
                    await TryRunSingleFlightAsync($"Preview: {profile.Name}", async ct =>
                    {
                        var snapshot = CloneProfile(profile);
                        var report = await _syncEngine.PreviewAsync(snapshot, ct).ConfigureAwait(false);
                        _latestReport = report;
                        _latestReportTitle = $"Preview: {profile.Name}";

                        var summary = $"Would Copy: {report.CopiedCount}, Unchanged: {report.UnchangedCount}, Failed: {report.FailedCount}";
                        var title = report.IsSuccess ? $"SnapSync Preview: {profile.Name} Completed" : $"SnapSync Preview: {profile.Name} Completed with errors";
                        _context.API.ShowMsg(title, summary);
                    });
                    return true;
                }
            });
        }

        results.Add(new Result
        {
            Title = "Edit profile",
            SubTitle = $"Open SnapSync settings to edit '{profile.Name}'",
            IcoPath = "Images\\app.png",
            Action = _ =>
            {
                _context.API.OpenSettingDialog();
                return true;
            }
        });

        if (_latestReport != null)
        {
            results.Add(new Result
            {
                Title = "View last report",
                SubTitle = $"Inspect cached report for '{_latestReportTitle}'",
                IcoPath = "Images\\app.png",
                Action = _ =>
                {
                    _context.API.ChangeQuery("sy :report ");
                    return false;
                }
            });
        }

        return results;
    }

    /// <inheritdoc/>
    public Control CreateSettingPanel()
    {
        Action safeSaveAction = () =>
        {
            if (_configurationLoadError != null)
            {
                throw new InvalidOperationException($"Cannot save configuration while load error exists: {_configurationLoadError}. Please repair the JSON configuration manually to avoid data loss.");
            }
            _context.API.SaveSettingJsonStorage<SnapSyncConfiguration>();
        };

        var viewModel = new SnapSyncSettingsViewModel(
            _configuration,
            safeSaveAction,
            _syncEngine);

        if (_configurationLoadError != null)
        {
            viewModel.StatusColor = System.Windows.Media.Brushes.Red;
            viewModel.StatusMessage = $"PROTECTED: {_configurationLoadError}. Saving is disabled to prevent overwriting existing configuration.";
        }

        return new SnapSyncSettingsView(viewModel);
    }

    private async Task TryRunSingleFlightAsync(string description, Func<CancellationToken, Task> operation)
    {
        CancellationTokenSource cts;

        lock (_operationLock)
        {
            if (_activeCts != null)
            {
                _context.API.ShowMsg("SnapSync Busy", $"An operation is already in progress: '{_activeOperationDescription}'. Concurrent operations are not allowed.");
                return;
            }

            _activeCts = new CancellationTokenSource();
            _activeOperationDescription = description;
            cts = _activeCts;
        }

        try
        {
            _context.API.LogInfo(nameof(SnapSync), $"Operation started: '{description}'.");
            await operation(cts.Token).ConfigureAwait(false);
            _context.API.LogInfo(nameof(SnapSync), $"Operation completed: '{description}'.");
        }
        catch (OperationCanceledException)
        {
            _context.API.LogWarn(nameof(SnapSync), $"Operation cancelled: '{description}'.");
            _context.API.ShowMsg("SnapSync Operation Cancelled", $"Operation '{description}' was cancelled. Partial outcomes were safely retained.");
        }
        catch (Exception ex)
        {
            _context.API.LogException(nameof(SnapSync), $"Operation failed: '{description}'", ex);
            _context.API.ShowMsg($"SnapSync Error: {description}", ex.Message);
        }
        finally
        {
            lock (_operationLock)
            {
                if (ReferenceEquals(_activeCts, cts))
                {
                    _activeCts.Dispose();
                    _activeCts = null;
                    _activeOperationDescription = null;
                }
            }
        }
    }

    private void CancelActiveOperation()
    {
        lock (_operationLock)
        {
            if (_activeCts != null)
            {
                _activeCts.Cancel();
            }
        }
    }

    private static SnapSyncConfiguration CloneConfiguration(SnapSyncConfiguration config)
    {
        var json = JsonSerializer.Serialize(config);
        return JsonSerializer.Deserialize<SnapSyncConfiguration>(json) ?? new SnapSyncConfiguration();
    }

    private static SyncProfile CloneProfile(SyncProfile profile)
    {
        var json = JsonSerializer.Serialize(profile);
        return JsonSerializer.Deserialize<SyncProfile>(json) ?? new SyncProfile();
    }
}
