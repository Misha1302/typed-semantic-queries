using MiniCompiler.IR;
using SemanticContracts;
using SemanticQueries.Core;

namespace Packages.BranchLayout;

public sealed class BranchLayoutPass
{
    private static readonly BranchProbabilityQuery Query = new();

    public IReadOnlyList<string> Order(Branch branch, SemanticSession session)
    {
        var probability = session.Query(Query, branch.Id);
        if (probability.Status != QueryStatus.Known)
            return [branch.TrueBlock, branch.FalseBlock];

        return probability.Value >= 0.5
            ? [branch.TrueBlock, branch.FalseBlock]
            : [branch.FalseBlock, branch.TrueBlock];
    }
}
