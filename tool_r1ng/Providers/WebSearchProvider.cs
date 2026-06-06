using tool_r1ng.Core;
using tool_r1ng.Utilities;

namespace tool_r1ng.Providers;

public sealed class WebSearchProvider : tool_r1ng.Core.IQueryProvider
{
    public string Id => "web";

    public string Name => "Web";

    public ValueTask<IReadOnlyList<QueryResult>> QueryAsync(QueryContext context, CancellationToken cancellationToken)
    {
        if (context.IsEmpty)
        {
            return ValueTask.FromResult<IReadOnlyList<QueryResult>>(Array.Empty<QueryResult>());
        }

        var query = context.Query;
        var results = new List<QueryResult>();

        if (LooksLikeUrl(query))
        {
            var url = query.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                      || query.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                ? query
                : $"https://{query}";

            results.Add(new QueryResult
            {
                Title = url,
                Subtitle = "Open URL",
                IconGlyph = "\uE774",
                ProviderId = Id,
                ProviderName = Name,
                Score = 95,
                ExecuteAsync = _ => ProcessLauncher.OpenAsync(url)
            });
        }

        if (query.Length >= 2)
        {
            var searchText = query.StartsWith("?", StringComparison.Ordinal)
                ? query[1..].Trim()
                : query;

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                results.Add(new QueryResult
                {
                    Title = searchText,
                    Subtitle = "Search web",
                    IconGlyph = "\uE721",
                    ProviderId = Id,
                    ProviderName = Name,
                    Score = query.StartsWith("?", StringComparison.Ordinal) ? 100 : 12,
                    ExecuteAsync = _ => ProcessLauncher.OpenAsync(
                        $"https://www.bing.com/search?q={Uri.EscapeDataString(searchText)}")
                });
            }
        }

        return ValueTask.FromResult<IReadOnlyList<QueryResult>>(results);
    }

    private static bool LooksLikeUrl(string query)
    {
        return !query.Contains(' ')
               && (Uri.TryCreate(query, UriKind.Absolute, out var absolute)
                   && (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps)
                   || query.Contains('.') && !query.Any(char.IsWhiteSpace));
    }
}
