using MiniCompiler.IR;
using SemanticContracts;

namespace Baseline.DirectServices;

public interface IRangeService
{
    bool TryGet(RangeKey key, out AbstractRange range);
}

public interface ILengthService
{
    bool TryGet(LengthKey key, out int length);
}

public interface IInBoundsService
{
    bool IsProven(InBoundsKey key);
}

public interface IAlignmentService
{
    bool TryGet(AlignmentKey key, out int bytes);
}

public sealed class RangeRegistry(IEnumerable<IRangeService> providers)
{
    private readonly IRangeService[] _providers = providers.ToArray();

    public bool TryGet(RangeKey key, out AbstractRange range)
    {
        range = default;
        var found = false;
        foreach (var provider in _providers)
        {
            if (!provider.TryGet(key, out var candidate)) continue;
            if (!found)
            {
                range = candidate;
                found = true;
                continue;
            }

            if (!range.Intersects(candidate)) return false;
            range = range.Intersect(candidate);
        }

        return found;
    }
}

public sealed class BaselineBoundsOptimizer(IInBoundsService inBounds)
{
    public BoundsCheck Run(BoundsCheck check) =>
        inBounds.IsProven(new InBoundsKey(check.Array, check.Index, check.Point))
            ? check with { Removed = true }
            : check;
}
