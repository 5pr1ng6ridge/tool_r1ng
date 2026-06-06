namespace tool_r1ng.Core;

public sealed class QueryResult
{
    public required string Title { get; init; }

    public string Subtitle { get; init; } = string.Empty;

    public string IconGlyph { get; init; } = "\uE721";

    public double Score { get; init; }

    public required string ProviderId { get; init; }

    public required string ProviderName { get; init; }

    public required Func<CancellationToken, Task> ExecuteAsync { get; init; }
}
