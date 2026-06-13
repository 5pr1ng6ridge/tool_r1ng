using System.Windows.Media;

namespace tool_r1ng.Core;

public sealed class QueryResult
{
    public required string Title { get; init; }

    public IReadOnlyList<HighlightedTextSegment> HighlightedTitle { get; init; } = [];

    public bool HasHighlightedTitle => HighlightedTitle.Count > 0;

    public string Subtitle { get; init; } = string.Empty;

    public string IconGlyph { get; init; } = "\uE721";

    public ImageSource? IconImage { get; init; }

    public double Score { get; init; }

    public required string ProviderId { get; init; }

    public required string ProviderName { get; init; }

    public required Func<CancellationToken, Task> ExecuteAsync { get; init; }

    public bool DismissAfterExecute { get; init; } = true;

    public string SuccessStatusText { get; init; } = string.Empty;

    public Func<CancellationToken, Task>? InlineActionAsync { get; init; }

    public string InlineActionGlyph { get; init; } = string.Empty;

    public string InlineActionToolTip { get; init; } = string.Empty;

    public string InlineActionSuccessStatusText { get; init; } = string.Empty;

    public bool HasInlineAction => InlineActionAsync is not null;

    public Func<CancellationToken, Task>? SecondaryActionAsync { get; init; }

    public string SecondaryActionGlyph { get; init; } = "\uE8B7";

    public string SecondaryActionToolTip { get; init; } = string.Empty;

    public bool HasSecondaryAction => SecondaryActionAsync is not null;

    public LaunchHistoryEntry? LaunchHistoryEntry { get; init; }
}
