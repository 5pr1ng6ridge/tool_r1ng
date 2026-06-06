using System.Threading;
using System.Threading.Tasks;

namespace tool_r1ng.Core;

public interface IQueryProvider
{
    string Id { get; }

    string Name { get; }

    ValueTask<IReadOnlyList<QueryResult>> QueryAsync(QueryContext context, CancellationToken cancellationToken);
}
