using tool_r1ng.Core;

namespace tool_r1ng.Services;

public sealed class LauncherEngine
{
    private readonly IReadOnlyList<tool_r1ng.Core.IQueryProvider> _providers;

    public LauncherEngine(IEnumerable<tool_r1ng.Core.IQueryProvider> providers)
    {
        _providers = providers.ToList();
    }

    public async Task<IReadOnlyList<QueryResult>> SearchAsync(string rawQuery, CancellationToken cancellationToken)
    {
        var context = new QueryContext(rawQuery);
        var providerTasks = _providers.Select(provider => QueryProviderAsync(provider, context, cancellationToken));
        var providerResults = await Task.WhenAll(providerTasks);

        return providerResults
            .SelectMany(results => results)
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.Title)
            .Take(12)
            .ToList();
    }

    private static async Task<IReadOnlyList<QueryResult>> QueryProviderAsync(
        tool_r1ng.Core.IQueryProvider provider,
        QueryContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await provider.QueryAsync(context, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return Array.Empty<QueryResult>();
        }
    }
}
