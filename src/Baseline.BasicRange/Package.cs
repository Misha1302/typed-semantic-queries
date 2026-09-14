using Baseline.DirectServices;
using MiniCompiler.IR;
using SemanticContracts;

namespace Baseline.BasicRange;

public sealed class BasicRangeService(CompilationUnit unit) : IRangeService
{
    public bool TryGet(RangeKey key, out AbstractRange range)
    {
        if (unit.TryGetConstant(key.Value, out var value))
        {
            range = new AbstractRange(value, value);
            return true;
        }

        range = default;
        return false;
    }
}
