using System.Globalization;
using System.Windows;
using tool_r1ng.Core;
using tool_r1ng.Utilities;

namespace tool_r1ng.Providers;

public sealed class CalculatorProvider : tool_r1ng.Core.IQueryProvider
{
    public string Id => "calculator";

    public string Name => "Calculator";

    public ValueTask<IReadOnlyList<QueryResult>> QueryAsync(QueryContext context, CancellationToken cancellationToken)
    {
        var expression = context.Query.StartsWith("=", StringComparison.Ordinal)
            ? context.Query[1..].Trim()
            : context.Query;

        if (!LooksLikeExpression(expression)
            || !ExpressionEvaluator.TryEvaluate(expression, out var value))
        {
            return ValueTask.FromResult<IReadOnlyList<QueryResult>>(Array.Empty<QueryResult>());
        }

        var formattedValue = FormatNumber(value);
        IReadOnlyList<QueryResult> results =
        [
            new QueryResult
            {
                Title = formattedValue,
                Subtitle = expression,
                IconGlyph = "\uE8EF",
                ProviderId = Id,
                ProviderName = Name,
                Score = context.Query.StartsWith("=", StringComparison.Ordinal) ? 120 : 90,
                ExecuteAsync = _ =>
                {
                    System.Windows.Clipboard.SetText(formattedValue);
                    return Task.CompletedTask;
                }
            }
        ];

        return ValueTask.FromResult(results);
    }

    private static bool LooksLikeExpression(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return false;
        }

        var hasDigit = expression.Any(char.IsDigit);
        var hasOperator = expression.Any(character => "+-*/()".Contains(character));
        return hasDigit && hasOperator;
    }

    private static string FormatNumber(double value)
    {
        return Math.Abs(value % 1) < double.Epsilon
            ? value.ToString("0", CultureInfo.InvariantCulture)
            : value.ToString("0.##########", CultureInfo.InvariantCulture);
    }
}
