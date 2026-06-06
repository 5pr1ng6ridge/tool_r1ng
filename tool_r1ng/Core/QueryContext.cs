namespace tool_r1ng.Core;

public sealed record QueryContext(string RawQuery)
{
    public string Query => RawQuery.Trim();

    public bool IsEmpty => string.IsNullOrWhiteSpace(Query);

    public IReadOnlyList<string> Tokens =>
        Query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
