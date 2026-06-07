using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using tool_r1ng.Core;

namespace tool_r1ng.Controls;

public sealed class HighlightedTextBlock : TextBlock
{
    public static readonly DependencyProperty SegmentsProperty =
        DependencyProperty.Register(
            nameof(Segments),
            typeof(IReadOnlyList<HighlightedTextSegment>),
            typeof(HighlightedTextBlock),
            new PropertyMetadata(Array.Empty<HighlightedTextSegment>(), OnContentChanged));

    public static readonly DependencyProperty PlainTextProperty =
        DependencyProperty.Register(
            nameof(PlainText),
            typeof(string),
            typeof(HighlightedTextBlock),
            new PropertyMetadata(string.Empty, OnContentChanged));

    public IReadOnlyList<HighlightedTextSegment> Segments
    {
        get => (IReadOnlyList<HighlightedTextSegment>)GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }

    public string PlainText
    {
        get => (string)GetValue(PlainTextProperty);
        set => SetValue(PlainTextProperty, value);
    }

    private static void OnContentChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        ((HighlightedTextBlock)dependencyObject).UpdateInlines();
    }

    private void UpdateInlines()
    {
        Inlines.Clear();

        if (Segments.Count == 0)
        {
            Inlines.Add(new Run(PlainText));
            return;
        }

        foreach (var segment in Segments)
        {
            Inlines.Add(new Run(segment.Text)
            {
                FontWeight = segment.IsMatch ? FontWeights.Bold : FontWeights.Normal
            });
        }
    }
}
