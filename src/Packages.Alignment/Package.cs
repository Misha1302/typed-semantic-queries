using MiniCompiler.IR;
using SemanticContracts;
using SemanticQueries.Core;

namespace Packages.Alignment;

public sealed class ExplicitAlignmentProvider(CompilationUnit unit)
    : IQueryProvider<AlignmentQuery, AlignmentKey, int>
{
    public QueryResult<int> TryGet(AlignmentKey key, QueryContext context) =>
        unit.TryGetAlignment(key.Value, key.Point, out var bytes)
            ? QueryResult<int>.Known(bytes)
            : QueryResult<int>.Unknown;
}
