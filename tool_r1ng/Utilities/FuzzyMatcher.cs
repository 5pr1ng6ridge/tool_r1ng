namespace tool_r1ng.Utilities;

public static class FuzzyMatcher
{
    public static double Score(string candidate, string query)
    {
        if (string.IsNullOrWhiteSpace(candidate) || string.IsNullOrWhiteSpace(query))
        {
            return 0;
        }

        var normalizedCandidate = candidate.Trim().ToLowerInvariant();
        var normalizedQuery = query.Trim().ToLowerInvariant();

        var directScore = ScoreDirect(normalizedCandidate, normalizedQuery);
        if (directScore > 0)
        {
            return directScore;
        }

        var tokenScore = ScoreTokens(normalizedCandidate, normalizedQuery);
        var subsequenceScore = ScoreSubsequence(normalizedCandidate, normalizedQuery);
        return Math.Max(tokenScore, subsequenceScore);
    }

    private static double ScoreDirect(string candidate, string query)
    {
        if (candidate == query)
        {
            return 120;
        }

        if (candidate.StartsWith(query, StringComparison.Ordinal))
        {
            return 96 - Math.Min(20, candidate.Length - query.Length);
        }

        if (candidate.Contains(query, StringComparison.Ordinal))
        {
            return 78 - Math.Min(18, candidate.IndexOf(query, StringComparison.Ordinal));
        }

        return 0;
    }

    private static double ScoreTokens(string candidate, string query)
    {
        var tokens = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
        {
            return 0;
        }

        var candidateTokens = candidate.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var total = 0.0;

        foreach (var token in tokens)
        {
            var score = candidateTokens
                .Select(candidateToken => Math.Max(ScoreDirect(candidateToken, token), ScoreSubsequence(candidateToken, token)))
                .DefaultIfEmpty(0)
                .Max();

            if (score <= 0)
            {
                return 0;
            }

            total += score;
        }

        return total / tokens.Length * 0.9;
    }

    private static double ScoreSubsequence(string candidate, string query)
    {
        var queryIndex = 0;
        var consecutive = 0;
        var bestRun = 0;

        foreach (var character in candidate)
        {
            if (queryIndex < query.Length && character == query[queryIndex])
            {
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
            return 0;
        }

        return 42 + bestRun * 4 - Math.Min(20, candidate.Length - query.Length);
    }
}
