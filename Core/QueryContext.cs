namespace tool_r1ng.Core;

public sealed record QueryContext(string RawQuery)
{
    public string Query => RawQuery.Trim();

    public bool IsEmpty => string.IsNullOrWhiteSpace(Query);

    public bool IsControlQuery => Query.StartsWith("/", StringComparison.Ordinal);

    public bool IsForcedFileSearch => RawQuery.TrimStart().StartsWith(". ", StringComparison.Ordinal);

    public string FileSearchQuery
    {
        get
        {
            var trimmedStart = RawQuery.TrimStart();
            return IsForcedFileSearch
                ? trimmedStart[2..].Trim()
                : Query;
        }
    }

    public IReadOnlyList<string> Tokens =>
        Query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
