using MiniCompiler.IR;
using SemanticContracts;
using SemanticQueries.Core;

namespace Packages.Effects;

public sealed class LoopEffectsProvider(CompilationUnit unit)
    : IQueryProvider<EffectsQuery, EffectsKey, MemoryEffects>
{
    public QueryResult<MemoryEffects> TryGet(EffectsKey key, QueryContext context) =>
        unit.TryGetLoopWrites(key.LoopPoint, out var writes)
            ? QueryResult<MemoryEffects>.Known(new MemoryEffects(writes))
            : QueryResult<MemoryEffects>.Unknown;
}
