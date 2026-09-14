using SemanticContracts;
using SemanticQueries.Core;

namespace Packages.LicmBridge;

public sealed class CanHoistBridgeProvider
    : IQueryProvider<CanHoistQuery, CanHoistKey, Proof>
{
    private static readonly EffectsQuery Effects = new();
    private static readonly NoAliasQuery NoAlias = new();

    public QueryResult<Proof> TryGet(CanHoistKey key, QueryContext context)
    {
        var effects = context.Query(Effects, new EffectsKey(key.LoopPoint));
        if (effects.Status != QueryStatus.Known)
            return QueryResult<Proof>.Unknown;

        foreach (var write in effects.Value!.Writes)
        {
            if (write == key.Load) return QueryResult<Proof>.Unknown;
            var noAlias = context.Query(NoAlias, new NoAliasKey(key.Load, write, key.LoopPoint));
            if (noAlias.Status != QueryStatus.Known || !noAlias.Value!.Proven)
                return QueryResult<Proof>.Unknown;
        }

        return QueryResult<Proof>.Known(Proof.Yes);
    }
}
