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
        var providers = context.IsControlQuery
            ? _providers.Where(provider => provider.Id.Equals("controls", StringComparison.OrdinalIgnoreCase))
            : _providers.Where(provider => !provider.Id.Equals("controls", StringComparison.OrdinalIgnoreCase));
        var providerTasks = providers.Select(provider => QueryProviderAsync(provider, context, cancellationToken));
        var providerResults = await Task.WhenAll(providerTasks);

        return providerResults
            .SelectMany(results => results)
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.Title)
            .Take(12)
            .ToList();
    }

    public async Task WarmUpAsync(CancellationToken cancellationToken)
    {
        var providerTasks = _providers
            .OfType<IWarmUpProvider>()
            .Select(provider => WarmUpProviderAsync(provider, cancellationToken));

        await Task.WhenAll(providerTasks);
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

    private static async Task WarmUpProviderAsync(IWarmUpProvider provider, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await provider.WarmUpAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
        }
    }
}
