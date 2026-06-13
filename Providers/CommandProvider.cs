using tool_r1ng.Core;
using tool_r1ng.Utilities;

namespace tool_r1ng.Providers;

public sealed class CommandProvider : tool_r1ng.Core.IQueryProvider
{
    public string Id => "command";

    public string Name => "Command";

    public ValueTask<IReadOnlyList<QueryResult>> QueryAsync(QueryContext context, CancellationToken cancellationToken)
    {
        if (!context.IsCommandQuery || string.IsNullOrWhiteSpace(context.CommandQuery))
        {
            return ValueTask.FromResult<IReadOnlyList<QueryResult>>(Array.Empty<QueryResult>());
        }

        var command = context.CommandQuery;
        var closeAfterExecute = context.CommandClosesAfterExecute;
        IReadOnlyList<QueryResult> results =
        [
            new QueryResult
            {
                Title = command,
                HighlightedTitle = HighlightBuilder.Build(command, Enumerable.Range(0, command.Length).ToArray()),
                Subtitle = closeAfterExecute
                    ? "Run in PowerShell and close"
                    : "Run in PowerShell",
                IconGlyph = "\uE756",
                ProviderId = Id,
                ProviderName = Name,
                Score = 320,
                ExecuteAsync = _ => ProcessLauncher.RunCommandAsync(command, closeAfterExecute),
                LaunchHistoryEntry = new LaunchHistoryEntry(
                    command,
                    command,
                    string.Empty,
                    string.Empty,
                    closeAfterExecute ? "PowerShell - closes after run" : "PowerShell",
                    LaunchHistoryKinds.Command,
                    closeAfterExecute)
            }
        ];

        return ValueTask.FromResult(results);
    }
}
