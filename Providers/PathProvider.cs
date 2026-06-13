using System.IO;
using tool_r1ng.Core;
using tool_r1ng.Utilities;

namespace tool_r1ng.Providers;

public sealed class PathProvider : tool_r1ng.Core.IQueryProvider
{
    public string Id => "path";

    public string Name => "Path";

    public ValueTask<IReadOnlyList<QueryResult>> QueryAsync(QueryContext context, CancellationToken cancellationToken)
    {
        if (context.IsProviderExclusiveQuery || context.IsEmpty)
        {
            return ValueTask.FromResult<IReadOnlyList<QueryResult>>(Array.Empty<QueryResult>());
        }

        var path = NormalizePath(context.Query);
        if (string.IsNullOrWhiteSpace(path))
        {
            return ValueTask.FromResult<IReadOnlyList<QueryResult>>(Array.Empty<QueryResult>());
        }

        if (path.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
        {
            return ValueTask.FromResult<IReadOnlyList<QueryResult>>(
                [CreateShellResult(path)]);
        }

        if (!File.Exists(path) && !Directory.Exists(path))
        {
            return ValueTask.FromResult<IReadOnlyList<QueryResult>>(Array.Empty<QueryResult>());
        }

        return ValueTask.FromResult<IReadOnlyList<QueryResult>>(
            [CreateFileSystemResult(path)]);
    }

    private static QueryResult CreateShellResult(string path)
    {
        return new QueryResult
        {
            Title = path,
            Subtitle = "Open shell path",
            IconGlyph = "\uE8B7",
            ProviderId = "path",
            ProviderName = "Path",
            Score = 250,
            ExecuteAsync = _ => ProcessLauncher.OpenAsync(path),
            LaunchHistoryEntry = new LaunchHistoryEntry(
                path,
                path,
                string.Empty,
                string.Empty,
                "Shell path",
                LaunchHistoryKinds.Path)
        };
    }

    private static QueryResult CreateFileSystemResult(string path)
    {
        var isFolder = Directory.Exists(path);
        var title = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(title))
        {
            title = path;
        }

        return new QueryResult
        {
            Title = title,
            Subtitle = path,
            IconGlyph = isFolder ? "\uE8B7" : "\uE8A5",
            IconImage = IconLoader.LoadAssociatedIcon(path),
            ProviderId = "path",
            ProviderName = "Path",
            Score = 250,
            ExecuteAsync = _ => ProcessLauncher.OpenAsync(path),
            SecondaryActionGlyph = "\uE8B7",
            SecondaryActionToolTip = "Open containing folder",
            SecondaryActionAsync = _ => ProcessLauncher.OpenContainingFolderAsync(path),
            LaunchHistoryEntry = new LaunchHistoryEntry(
                title,
                path,
                path,
                path,
                isFolder ? "Folder path" : "File path",
                LaunchHistoryKinds.Path)
        };
    }

    private static string NormalizePath(string query)
    {
        var value = query.Trim().Trim('"');
        if (value.Length == 0)
        {
            return string.Empty;
        }

        if (value.StartsWith("~", StringComparison.Ordinal))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            value = home + value[1..];
        }

        return Environment.ExpandEnvironmentVariables(value);
    }
}
