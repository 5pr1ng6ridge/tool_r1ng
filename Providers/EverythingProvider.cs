using System.IO;
using tool_r1ng.Core;
using tool_r1ng.Utilities;

namespace tool_r1ng.Providers;

public sealed class EverythingProvider : tool_r1ng.Core.IQueryProvider, IWarmUpProvider
{
    private const int MaxResults = 6;
    private const int MinQueryLength = 2;
    private readonly LauncherSettings _settings;

    public EverythingProvider(LauncherSettings settings)
    {
        _settings = settings;
    }

    public string Id => "everything";

    public string Name => "Files";

    public async Task WarmUpAsync(CancellationToken cancellationToken)
    {
        if (!_settings.EnableEverythingFileSearch)
        {
            return;
        }

        try
        {
            await Task.Run(async () =>
            {
                _ = await QueryAsync(new QueryContext("test"), cancellationToken);
            }, cancellationToken);
        }
        catch
        {
        }
    }

    public ValueTask<IReadOnlyList<QueryResult>> QueryAsync(QueryContext context, CancellationToken cancellationToken)
    {
        var isForced = context.IsForcedFileSearch;
        if (!_settings.EnableEverythingFileSearch && !isForced)
        {
            return ValueTask.FromResult<IReadOnlyList<QueryResult>>(Array.Empty<QueryResult>());
        }

        var query = context.FileSearchQuery;
        var minQueryLength = isForced ? 1 : MinQueryLength;
        if (query.Length < minQueryLength)
        {
            return ValueTask.FromResult<IReadOnlyList<QueryResult>>(Array.Empty<QueryResult>());
        }

        try
        {
            var results = EverythingClient.Search(query, MaxResults, cancellationToken)
                .Select(result => CreateResult(result, query, isForced))
                .ToList();

            return ValueTask.FromResult<IReadOnlyList<QueryResult>>(results);
        }
        catch
        {
            return ValueTask.FromResult<IReadOnlyList<QueryResult>>(Array.Empty<QueryResult>());
        }
    }

    private QueryResult CreateResult(EverythingSearchResult result, string query, bool isForced)
    {
        var title = Path.GetFileName(result.FullPath);
        if (string.IsNullOrWhiteSpace(title))
        {
            title = result.FullPath;
        }

        var match = FuzzyMatcher.Match(title, query);

        return new QueryResult
        {
            Title = title,
            HighlightedTitle = HighlightBuilder.Build(title, match.MatchedIndices),
            Subtitle = result.FullPath,
            IconGlyph = result.IsFolder ? "\uE8B7" : "\uE8A5",
            IconImage = IconLoader.LoadAssociatedIcon(result.FullPath),
            ProviderId = Id,
            ProviderName = Name,
            Score = (isForced ? 240 : 62) + Math.Min(24, match.Score * 0.2),
            ExecuteAsync = _ => ProcessLauncher.OpenAsync(result.FullPath),
            SecondaryActionGlyph = "\uE8B7",
            SecondaryActionToolTip = "Open containing folder",
            SecondaryActionAsync = _ => ProcessLauncher.OpenContainingFolderAsync(result.FullPath)
        };
    }
}
