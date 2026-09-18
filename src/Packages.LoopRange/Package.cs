using MiniCompiler.IR;
using SemanticContracts;
using SemanticQueries.Core;

namespace Packages.LoopRange;

public sealed class LoopAnalysisEngine(CompilationUnit unit)
{
    public bool TryGetInductionRange(RangeKey key, out AbstractRange range)
    {
        if (!unit.TryGetLoop(key.Value, key.Point, out var loop))
        {
            range = default;
            return false;
        }

        range = new AbstractRange(loop.StartInclusive, loop.EndExclusive - 1);
        return true;
    }
}

public sealed class LoopRangeProvider : IQueryProvider<RangeQuery, RangeKey, AbstractRange>, IStableQueryProvider
{
    private readonly LoopAnalysisEngine _engine;

    public LoopRangeProvider(CompilationUnit unit) : this(new LoopAnalysisEngine(unit)) { }

    public LoopRangeProvider(LoopAnalysisEngine engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
    }

    public QueryResult<AbstractRange> TryGet(RangeKey key, QueryContext context) =>
        _engine.TryGetInductionRange(key, out var range)
            ? QueryResult<AbstractRange>.Known(range)
            : QueryResult<AbstractRange>.Unknown;
}
