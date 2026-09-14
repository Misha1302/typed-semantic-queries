using Baseline.DirectServices;
using MiniCompiler.IR;
using SemanticContracts;

namespace Baseline.LoopRange;

public sealed class LoopRangeService(CompilationUnit unit) : IRangeService
{
    public bool TryGet(RangeKey key, out AbstractRange range)
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
