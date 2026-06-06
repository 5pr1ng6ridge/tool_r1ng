using System.IO;
using tool_r1ng.Core;
using tool_r1ng.Utilities;

namespace tool_r1ng.Providers;

public sealed class ApplicationsProvider : tool_r1ng.Core.IQueryProvider
{
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private IReadOnlyList<ApplicationEntry>? _cachedApps;

    public string Id => "applications";

    public string Name => "Apps";

    public async ValueTask<IReadOnlyList<QueryResult>> QueryAsync(QueryContext context, CancellationToken cancellationToken)
    {
        var apps = await EnsureCacheAsync(cancellationToken);

        if (context.IsEmpty)
        {
            return apps
                .OrderBy(app => app.Name)
                .Take(8)
                .Select((app, index) => CreateResult(app, 80 - index))
                .ToList();
        }

        return apps
            .Select(app => new
            {
                App = app,
                Score = Math.Max(
                    FuzzyMatcher.Score(app.Name, context.Query),
                    FuzzyMatcher.Score(app.SearchText, context.Query) * 0.75)
            })
            .Where(item => item.Score >= 28)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.App.Name)
            .Take(8)
            .Select(item => CreateResult(item.App, item.Score))
            .ToList();
    }

    private QueryResult CreateResult(ApplicationEntry app, double score)
    {
        return new QueryResult
        {
            Title = app.Name,
            Subtitle = app.Location,
            IconGlyph = "\uECAA",
            ProviderId = Id,
            ProviderName = Name,
            Score = score,
            ExecuteAsync = _ => ProcessLauncher.OpenAsync(app.ShortcutPath)
        };
    }

    private async Task<IReadOnlyList<ApplicationEntry>> EnsureCacheAsync(CancellationToken cancellationToken)
    {
        if (_cachedApps is not null)
        {
            return _cachedApps;
        }

        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            if (_cachedApps is not null)
            {
                return _cachedApps;
            }

            _cachedApps = await Task.Run(LoadApplications, cancellationToken);
            return _cachedApps;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private static IReadOnlyList<ApplicationEntry> LoadApplications()
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)
        };

        var entries = new List<ApplicationEntry>();

        foreach (var root in roots.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var file in EnumerateLaunchableFiles(root))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                entries.Add(new ApplicationEntry(
                    name,
                    file,
                    GetRelativeLocation(root, file)));
            }
        }

        return entries
            .GroupBy(app => app.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderBy(app => app.ShortcutPath.Length).First())
            .OrderBy(app => app.Name)
            .ToList();
    }

    private static IEnumerable<string> EnumerateLaunchableFiles(string root)
    {
        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories);
        }
        catch
        {
            yield break;
        }

        foreach (var file in files)
        {
            var extension = Path.GetExtension(file);
            if (extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".appref-ms", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".url", StringComparison.OrdinalIgnoreCase))
            {
                yield return file;
            }
        }
    }

    private static string GetRelativeLocation(string root, string file)
    {
        try
        {
            var relative = Path.GetRelativePath(root, file);
            return Path.GetDirectoryName(relative) is { Length: > 0 } directory
                ? directory
                : "Start menu";
        }
        catch
        {
            return Path.GetDirectoryName(file) ?? string.Empty;
        }
    }

    private sealed record ApplicationEntry(string Name, string ShortcutPath, string Location)
    {
        public string SearchText => $"{Name} {Location}";
    }
}
