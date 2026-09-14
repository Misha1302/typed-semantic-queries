using MiniCompiler.IR;
using SemanticContracts;
using SemanticQueries.Core;

namespace Packages.BoundsOptimizer;

public sealed class BoundsCheckOptimizer
{
    private static readonly InBoundsQuery Query = new();

    public BoundsCheck Run(BoundsCheck check, SemanticSession session)
    {
        var result = session.Query(Query, new InBoundsKey(check.Array, check.Index, check.Point));
        return result.Status == QueryStatus.Known && result.Value!.Proven
            ? check with { Removed = true }
            : check;
    }
}
