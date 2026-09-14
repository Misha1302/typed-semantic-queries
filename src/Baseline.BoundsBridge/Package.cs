using Baseline.DirectServices;
using SemanticContracts;

namespace Baseline.BoundsBridge;

public sealed class BaselineBoundsBridge(RangeRegistry ranges, ILengthService lengths) : IInBoundsService
{
    public bool IsProven(InBoundsKey key)
    {
        if (!ranges.TryGet(new RangeKey(key.Index, key.Point), out var range)) return false;
        if (!lengths.TryGet(new LengthKey(key.Array, key.Point), out var length)) return false;
        return range.Lower >= 0 && range.Upper < length;
    }
}
