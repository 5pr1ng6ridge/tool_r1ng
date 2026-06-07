using tool_r1ng.Core;

namespace tool_r1ng.Utilities;

public static class HighlightBuilder
{
    public static IReadOnlyList<HighlightedTextSegment> Build(string text, IReadOnlyList<int> matchedIndices)
    {
        if (string.IsNullOrEmpty(text) || matchedIndices.Count == 0)
        {
            return [];
        }

        var matched = matchedIndices
            .Where(index => index >= 0 && index < text.Length)
            .ToHashSet();
        var segments = new List<HighlightedTextSegment>();
        var start = 0;
        var isMatch = matched.Contains(0);

        for (var index = 1; index < text.Length; index++)
        {
            var nextIsMatch = matched.Contains(index);
            if (nextIsMatch == isMatch)
            {
                continue;
            }

            segments.Add(new HighlightedTextSegment(text[start..index], isMatch));
            start = index;
            isMatch = nextIsMatch;
        }

        segments.Add(new HighlightedTextSegment(text[start..], isMatch));
        return segments;
    }
}
