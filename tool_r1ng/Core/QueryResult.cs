using System.Windows.Media;

namespace tool_r1ng.Core;

public sealed class QueryResult
{
    public required string Title { get; init; }

    public string Subtitle { get; init; } = string.Empty;

    public string IconGlyph { get; init; } = "\uE721";

    public ImageSource? IconImage { get; init; }

    public double Score { get; init; }

    public required string ProviderId { get; init; }

    public required string ProviderName { get; init; }

    public required Func<CancellationToken, Task> ExecuteAsync { get; init; }

    public Func<CancellationToken, Task>? SecondaryActionAsync { get; init; }

    public string SecondaryActionToolTip { get; init; } = string.Empty;

    public bool HasSecondaryAction => SecondaryActionAsync is not null;
}
