using SemanticContracts;
using SemanticQueries.Core;

namespace Packages.BoundsBridge;

public sealed class BoundsBridgeProvider
    : IQueryProvider<InBoundsQuery, InBoundsKey, Proof>
{
    private static readonly RangeQuery Range = new();
    private static readonly LengthQuery Length = new();

    public QueryResult<Proof> TryGet(InBoundsKey key, QueryContext context)
    {
        var range = context.Query(Range, new RangeKey(key.Index, key.Point));
        var length = context.Query(Length, new LengthKey(key.Array, key.Point));
        if (range.Status != QueryStatus.Known || length.Status != QueryStatus.Known)
            return QueryResult<Proof>.Unknown;

        return range.Value!.Lower >= 0 && range.Value.Upper < length.Value
            ? QueryResult<Proof>.Known(Proof.Yes)
            : QueryResult<Proof>.Unknown;
    }
}
