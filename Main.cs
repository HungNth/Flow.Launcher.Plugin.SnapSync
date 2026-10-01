using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Plugin.SnapSync;

public class Main : IAsyncPlugin
{
    private PluginInitContext _context = null!;

    public Task InitAsync(PluginInitContext context)
    {
        _context = context;
        return Task.CompletedTask;
    }

    public Task<List<Result>> QueryAsync(Query query, CancellationToken token)
    {
        var results = new List<Result>
        {
            new()
            {
                Title = "SnapSync",
                SubTitle = string.IsNullOrWhiteSpace(query.Search)
                    ? "Type to search or sync"
                    : $"Query: {query.Search}",
                IcoPath = "Images\\app.png",
                Action = _ =>
                {
                    _context.API.ShowMsg("SnapSync", "SnapSync activated");
                    return true;
                }
            }
        };

        return Task.FromResult(results);
    }
}
