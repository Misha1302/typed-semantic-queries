using MiniCompiler.IR;
using SemanticContracts;
using SemanticQueries.Core;

namespace Packages.NoAlias;

public sealed class ExplicitNoAliasProvider(CompilationUnit unit)
    : IQueryProvider<NoAliasQuery, NoAliasKey, Proof>, IStableQueryProvider
{
    public QueryResult<Proof> TryGet(NoAliasKey key, QueryContext context) =>
        unit.IsKnownNoAlias(key.Left, key.Right, key.Point)
            ? QueryResult<Proof>.Known(Proof.Yes)
            : QueryResult<Proof>.Unknown;
}
