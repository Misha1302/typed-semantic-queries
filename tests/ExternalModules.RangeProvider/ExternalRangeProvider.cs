using SemanticContracts;
using SemanticQueries.Core;

namespace ExternalModules.RangeProvider;

public sealed class ExternalRangeProvider : IQueryProvider<RangeQuery, RangeKey, AbstractRange>
{
    public QueryResult<AbstractRange> TryGet(RangeKey key, QueryContext context) =>
        key.Value.Name == "i" && key.Point.Name == "body"
            ? QueryResult<AbstractRange>.Known(new AbstractRange(0, 3))
            : QueryResult<AbstractRange>.Unknown;
}
