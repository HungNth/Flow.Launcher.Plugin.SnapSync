using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.SnapSync.ViewModels;
using Flow.Launcher.Plugin.SnapSync.Views;
using SnapSync.Core;

namespace Flow.Launcher.Plugin.SnapSync;

/// <summary>
/// Flow Launcher plugin entry point for SnapSync.
/// </summary>
public class Main : IAsyncPlugin, ISettingProvider
{
    private PluginInitContext _context = null!;
    private SnapSyncConfiguration _configuration = null!;
    private readonly ISyncEngine _syncEngine = new SyncEngine();

    /// <inheritdoc/>
    public Task InitAsync(PluginInitContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _configuration = _context.API.LoadSettingJsonStorage<SnapSyncConfiguration>();

        if (_configuration.Profiles == null)
        {
            _configuration.Profiles = [];
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<List<Result>> QueryAsync(Query query, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        var results = new List<Result>();
        var searchTerm = query.Search?.Trim() ?? string.Empty;

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

        foreach (var profile in _configuration.Profiles)
        {
            token.ThrowIfCancellationRequested();

            var score = 100;
            if (!string.IsNullOrEmpty(searchTerm))
            {
                var match = _context.API.FuzzySearch(searchTerm, profile.Name);
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
                    ? $"Press Enter to synchronize ({itemCount} item(s))"
                    : $"{profile.Description} ({itemCount} item(s))";

            results.Add(new Result
            {
                Title = profile.Name,
                SubTitle = subTitle,
                IcoPath = "Images\\app.png",
                Score = score,
                AsyncAction = async actionContext =>
                {
                    if (!profile.Enabled)
                    {
                        _context.API.OpenSettingDialog();
                        return true;
                    }

                    await SynchronizeProfileAsync(profile);
                    return true;
                }
            });
        }

        return Task.FromResult(results);
    }

    /// <inheritdoc/>
    public Control CreateSettingPanel()
    {
        var viewModel = new SnapSyncSettingsViewModel(
            _configuration,
            () => _context.API.SaveSettingJsonStorage<SnapSyncConfiguration>());

        return new SnapSyncSettingsView(viewModel);
    }

    private async Task SynchronizeProfileAsync(SyncProfile profile)
    {
        try
        {
            var report = await _syncEngine.SynchronizeAsync(profile).ConfigureAwait(false);
            var summary = $"Copied: {report.CopiedCount}, Unchanged: {report.UnchangedCount}, Failed: {report.FailedCount}";
            var title = report.IsSuccess ? $"SnapSync: {profile.Name} Completed" : $"SnapSync: {profile.Name} Completed with errors";
            _context.API.ShowMsg(title, summary, "Images\\app.png");
        }
        catch (Exception ex)
        {
            _context.API.ShowMsg($"SnapSync: {profile.Name} Failed", ex.Message, "Images\\app.png");
        }
    }
}
