using tool_r1ng.Core;
using tool_r1ng.Services;
using tool_r1ng.Utilities;

namespace tool_r1ng.Providers;

public sealed class ApplicationHistoryProvider : tool_r1ng.Core.IQueryProvider
{
    public string Id => "application-history";

    public string Name => "History";

    public ValueTask<IReadOnlyList<QueryResult>> QueryAsync(QueryContext context, CancellationToken cancellationToken)
    {
        if (!context.IsHistoryQuery)
        {
            return ValueTask.FromResult<IReadOnlyList<QueryResult>>(Array.Empty<QueryResult>());
        }

        var query = context.HistoryQuery;
        var results = LaunchHistoryStore.Load()
            .Select(entry => new
            {
                Entry = entry,
                Match = string.IsNullOrWhiteSpace(query)
                    ? new FuzzyMatchResult(80, [])
                    : FuzzyMatcher.Match(entry.SearchText, query)
            })
            .Where(item => string.IsNullOrWhiteSpace(query) || item.Match.Score >= 24)
            .OrderByDescending(item => item.Match.Score + Math.Min(28, item.Entry.UseCount * 4))
            .ThenByDescending(item => item.Entry.LastUsedUtc)
            .Take(10)
            .Select(item => CreateResult(item.Entry, query))
            .ToList();

        return ValueTask.FromResult<IReadOnlyList<QueryResult>>(results);
    }

    private static QueryResult CreateResult(LaunchHistoryEntry entry, string query)
    {
        var titleMatch = string.IsNullOrWhiteSpace(query)
            ? FuzzyMatchResult.Empty
            : FuzzyMatcher.Match(entry.Name, query);

        return new QueryResult
        {
            Title = entry.Name,
            HighlightedTitle = HighlightBuilder.Build(entry.Name, titleMatch.MatchedIndices),
            Subtitle = $"Recent {GetKindLabel(entry.Kind)} - {entry.Location}",
            IconGlyph = GetIconGlyph(entry.Kind),
            IconImage = IconLoader.LoadAssociatedIcon(entry.IconPath)
                ?? IconLoader.LoadAssociatedIcon(entry.LaunchPath),
            ProviderId = "application-history",
            ProviderName = "History",
            Score = 260 + Math.Min(36, entry.UseCount * 4),
            ExecuteAsync = _ => ExecuteHistoryEntryAsync(entry),
            SecondaryActionToolTip = "Open containing folder",
            SecondaryActionAsync = string.IsNullOrWhiteSpace(entry.FolderPath)
                ? null
                : _ => ProcessLauncher.OpenContainingFolderAsync(entry.FolderPath),
            LaunchHistoryEntry = entry
        };
    }

    private static Task ExecuteHistoryEntryAsync(LaunchHistoryEntry entry)
    {
        return entry.Kind switch
        {
            LaunchHistoryKinds.Command => ProcessLauncher.RunCommandAsync(
                entry.LaunchPath,
                entry.CloseCommandAfterExecute),
            LaunchHistoryKinds.EverythingSearch => ProcessLauncher.OpenEverythingSearchAsync(entry.LaunchPath),
            _ => ProcessLauncher.OpenAsync(entry.LaunchPath)
        };
    }

    private static string GetKindLabel(string kind)
    {
        return kind switch
        {
            LaunchHistoryKinds.Command => "command",
            LaunchHistoryKinds.EverythingSearch => "Everything search",
            LaunchHistoryKinds.Path => "path",
            _ => "app"
        };
    }

    private static string GetIconGlyph(string kind)
    {
        return kind switch
        {
            LaunchHistoryKinds.Command => "\uE756",
            LaunchHistoryKinds.EverythingSearch => "\uE721",
            LaunchHistoryKinds.Path => "\uE8B7",
            _ => "\uECAA"
        };
    }
}
