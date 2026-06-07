namespace tool_r1ng.Utilities;

public static class FuzzyMatcher
{
    public static double Score(string candidate, string query)
    {
        return Match(candidate, query).Score;
    }

    public static FuzzyMatchResult Match(string candidate, string query)
    {
        if (string.IsNullOrWhiteSpace(candidate) || string.IsNullOrWhiteSpace(query))
        {
            return FuzzyMatchResult.Empty;
        }

        var normalizedCandidate = candidate.ToLowerInvariant();
        var normalizedQuery = query.Trim().ToLowerInvariant();

        var directMatch = MatchDirect(normalizedCandidate, normalizedQuery);
        if (directMatch.Score > 0)
        {
            return directMatch;
        }

        var acronymMatch = MatchAcronym(candidate, normalizedQuery);
        var tokenMatch = MatchTokens(candidate, normalizedQuery);
        var subsequenceMatch = MatchSubsequence(normalizedCandidate, normalizedQuery);
        return new[] { acronymMatch, tokenMatch, subsequenceMatch }
            .OrderByDescending(match => match.Score)
            .First();
    }

    public static IReadOnlyList<int> MatchIndices(string candidate, string query)
    {
        return Match(candidate, query).MatchedIndices;
    }

    private static FuzzyMatchResult MatchDirect(string candidate, string query)
    {
        if (candidate == query)
        {
            return new FuzzyMatchResult(120, Enumerable.Range(0, candidate.Length).ToArray());
        }

        if (candidate.StartsWith(query, StringComparison.Ordinal))
        {
            return new FuzzyMatchResult(
                96 - Math.Min(20, candidate.Length - query.Length),
                Enumerable.Range(0, query.Length).ToArray());
        }

        var index = candidate.IndexOf(query, StringComparison.Ordinal);
        if (index >= 0)
        {
            return new FuzzyMatchResult(
                78 - Math.Min(18, index),
                Enumerable.Range(index, query.Length).ToArray());
        }

        return FuzzyMatchResult.Empty;
    }

    private static FuzzyMatchResult MatchTokens(string candidate, string query)
    {
        var tokens = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
        {
            return FuzzyMatchResult.Empty;
        }

        var candidateTokens = EnumerateTokens(candidate).ToArray();
        var total = 0.0;
        var matchedIndices = new HashSet<int>();

        foreach (var token in tokens)
        {
            var score = candidateTokens
                .Select(candidateToken =>
                {
                    var directMatch = MatchDirect(candidateToken.Text.ToLowerInvariant(), token);
                    var subsequenceMatch = MatchSubsequence(candidateToken.Text.ToLowerInvariant(), token);
                    var best = directMatch.Score >= subsequenceMatch.Score ? directMatch : subsequenceMatch;
                    return new FuzzyMatchResult(
                        best.Score,
                        best.MatchedIndices.Select(index => index + candidateToken.Start).ToArray());
                })
                .OrderByDescending(match => match.Score)
                .FirstOrDefault(FuzzyMatchResult.Empty);

            if (score.Score <= 0)
            {
                return FuzzyMatchResult.Empty;
            }

            total += score.Score;
            foreach (var index in score.MatchedIndices)
            {
                matchedIndices.Add(index);
            }
        }

        return new FuzzyMatchResult(total / tokens.Length * 0.9, matchedIndices.Order().ToArray());
    }

    private static FuzzyMatchResult MatchAcronym(string candidate, string query)
    {
        if (query.Length == 0)
        {
            return FuzzyMatchResult.Empty;
        }

        var initials = EnumerateTokens(candidate)
            .Where(token => token.Text.Length > 0)
            .Select(token => new { Character = char.ToLowerInvariant(token.Text[0]), Index = token.Start })
            .ToArray();

        if (initials.Length == 0 || query.Length > initials.Length)
        {
            return FuzzyMatchResult.Empty;
        }

        var queryIndex = 0;
        var matchedIndices = new List<int>();
        foreach (var initial in initials)
        {
            if (queryIndex < query.Length && initial.Character == query[queryIndex])
            {
                matchedIndices.Add(initial.Index);
                queryIndex++;
            }
        }

        if (queryIndex != query.Length)
        {
            return FuzzyMatchResult.Empty;
        }

        return new FuzzyMatchResult(108 - Math.Min(12, initials.Length - query.Length), matchedIndices);
    }

    private static FuzzyMatchResult MatchSubsequence(string candidate, string query)
    {
        var queryIndex = 0;
        var consecutive = 0;
        var bestRun = 0;
        var matchedIndices = new List<int>();

        for (var index = 0; index < candidate.Length; index++)
        {
            var character = candidate[index];
            if (queryIndex < query.Length && character == query[queryIndex])
            {
                matchedIndices.Add(index);
                queryIndex++;
                consecutive++;
                bestRun = Math.Max(bestRun, consecutive);
            }
            else
            {
                consecutive = 0;
            }
        }

        if (queryIndex != query.Length)
        {
            return FuzzyMatchResult.Empty;
        }

        return new FuzzyMatchResult(
            42 + bestRun * 4 - Math.Min(20, candidate.Length - query.Length),
            matchedIndices);
    }

    private static IEnumerable<TokenSpan> EnumerateTokens(string value)
    {
        var start = -1;

        for (var index = 0; index < value.Length; index++)
        {
            if (char.IsLetterOrDigit(value[index]))
            {
                if (start < 0)
                {
                    start = index;
                }

                continue;
            }

            if (start >= 0)
            {
                yield return new TokenSpan(value[start..index], start);
                start = -1;
            }
        }

        if (start >= 0)
        {
            yield return new TokenSpan(value[start..], start);
        }
    }

    private sealed record TokenSpan(string Text, int Start);
}

public sealed record FuzzyMatchResult(double Score, IReadOnlyList<int> MatchedIndices)
{
    public static FuzzyMatchResult Empty { get; } = new(0, []);
}
