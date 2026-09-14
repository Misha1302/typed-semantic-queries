using MiniCompiler.IR;
using SemanticContracts;
using SemanticQueries.Core;

namespace Packages.ArraySemantics;

public sealed class ArrayLengthProvider(CompilationUnit unit)
    : IQueryProvider<LengthQuery, LengthKey, int>
{
    public QueryResult<int> TryGet(LengthKey key, QueryContext context) =>
        unit.TryGetArrayLength(key.Array, out var length)
            ? QueryResult<int>.Known(length)
            : QueryResult<int>.Unknown;
}
