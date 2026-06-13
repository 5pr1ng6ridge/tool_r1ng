namespace tool_r1ng.Core;

public sealed record QueryContext(string RawQuery)
{
    private string TrimmedStart => RawQuery.TrimStart();

    public string Query => GetQueryText();

    public bool IsEmpty => string.IsNullOrWhiteSpace(Query);

    public bool IsControlQuery => Query.StartsWith("/", StringComparison.Ordinal);

    public bool IsForcedFileSearch => RawQuery.TrimStart().StartsWith(". ", StringComparison.Ordinal);

    public bool IsHistoryQuery => TrimmedStart == "?" || TrimmedStart.StartsWith("? ", StringComparison.Ordinal);

    public bool IsCommandQuery => TrimmedStart.StartsWith("> ", StringComparison.Ordinal);

    public bool IsWindowPriorityQuery => TrimmedStart == "<" || TrimmedStart.StartsWith("< ", StringComparison.Ordinal);

    public bool IsWindowsSettingsQuery => TrimmedStart == "$" || TrimmedStart.StartsWith("$ ", StringComparison.Ordinal);

    public bool IsProviderExclusiveQuery => IsHistoryQuery || IsCommandQuery || IsWindowPriorityQuery || IsWindowsSettingsQuery;

    public bool CommandClosesAfterExecute =>
        IsCommandQuery
        && !string.IsNullOrWhiteSpace(CommandQuery)
        && RawQuery.Length > 0
        && char.IsWhiteSpace(RawQuery[^1]);

    public string HistoryQuery => IsHistoryQuery ? GetPrefixedQueryText() : Query;

    public string CommandQuery => IsCommandQuery ? GetPrefixedQueryText() : Query;

    public string WindowsSettingsQuery => IsWindowsSettingsQuery ? GetPrefixedQueryText() : Query;

    public string FileSearchQuery
    {
        get
        {
            return IsForcedFileSearch
                ? TrimmedStart[2..].Trim()
                : Query;
        }
    }

    public IReadOnlyList<string> Tokens =>
        Query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private string GetQueryText()
    {
        if (IsHistoryQuery || IsCommandQuery || IsWindowPriorityQuery || IsWindowsSettingsQuery)
        {
            return GetPrefixedQueryText();
        }

        return RawQuery.Trim();
    }

    private string GetPrefixedQueryText()
    {
        return TrimmedStart.Length <= 1
            ? string.Empty
            : TrimmedStart[2..].Trim();
    }
}
