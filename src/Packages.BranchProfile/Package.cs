using MiniCompiler.IR;
using SemanticContracts;
using SemanticQueries.Core;

namespace Packages.BranchProfile;

public sealed class ProfileBranchProbabilityProvider(CompilationUnit unit)
    : IQueryProvider<BranchProbabilityQuery, BranchId, double>, IStableQueryProvider
{
    public QueryResult<double> TryGet(BranchId key, QueryContext context) =>
        unit.TryGetBranchProbability(key, out var probability)
            ? QueryResult<double>.Known(probability)
            : QueryResult<double>.Unknown;
}
