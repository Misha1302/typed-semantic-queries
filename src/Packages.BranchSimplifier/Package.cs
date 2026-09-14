using MiniCompiler.IR;
using SemanticContracts;
using SemanticQueries.Core;

namespace Packages.BranchSimplifier;

public sealed class BranchSimplificationPass
{
    private static readonly RangeQuery Query = new();

    public bool? IsAlwaysTrue(LessThanCondition condition, SemanticSession session)
    {
        var range = session.Query(Query, new RangeKey(condition.Value, condition.Point));
        if (range.Status != QueryStatus.Known) return null;
        if (range.Value!.Upper < condition.Constant) return true;
        if (range.Value.Lower >= condition.Constant) return false;
        return null;
    }
}
