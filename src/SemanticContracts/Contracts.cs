using MiniCompiler.IR;
using SemanticQueries.Core;

namespace SemanticContracts;

public readonly record struct AbstractRange(int Lower, int Upper)
{
    public bool Intersects(AbstractRange other) => Math.Max(Lower, other.Lower) <= Math.Min(Upper, other.Upper);
    public AbstractRange Intersect(AbstractRange other) => new(Math.Max(Lower, other.Lower), Math.Min(Upper, other.Upper));
}

public readonly record struct RangeKey(ValueId Value, ProgramPoint Point);
public readonly record struct LengthKey(ArrayId Array, ProgramPoint Point);
public readonly record struct InBoundsKey(ArrayId Array, ValueId Index, ProgramPoint Point);
public readonly record struct EffectsKey(ProgramPoint LoopPoint);
public readonly record struct NoAliasKey(MemoryLocation Left, MemoryLocation Right, ProgramPoint Point);
public readonly record struct CanHoistKey(MemoryLocation Load, ProgramPoint LoopPoint);
public readonly record struct AlignmentKey(ValueId Value, ProgramPoint Point);
public readonly record struct Proof(bool Proven) { public static Proof Yes => new(true); }
public sealed record MemoryEffects(IReadOnlyList<MemoryLocation> Writes);

public sealed class RangeQuery : IQuerySpec<RangeKey, AbstractRange>
{
    public string Name => "Range";

    public QueryResult<AbstractRange> Combine(IReadOnlyList<AbstractRange> values)
    {
        if (values.Count == 0) return QueryResult<AbstractRange>.Unknown;
        var range = values[0];
        for (var i = 1; i < values.Count; i++)
        {
            if (!range.Intersects(values[i])) return QueryResult<AbstractRange>.Conflict;
            range = range.Intersect(values[i]);
        }

        return QueryResult<AbstractRange>.Known(range);
    }

    public void Validate(AbstractRange value)
    {
        if (value.Lower > value.Upper)
            throw new QueryContractException("Range lower bound cannot exceed upper bound.");
    }
}

public sealed class LengthQuery : IQuerySpec<LengthKey, int>
{
    public string Name => "Length";

    public QueryResult<int> Combine(IReadOnlyList<int> values)
    {
        if (values.Count == 0) return QueryResult<int>.Unknown;
        return values.All(value => value == values[0])
            ? QueryResult<int>.Known(values[0])
            : QueryResult<int>.Conflict;
    }

    public void Validate(int value)
    {
        if (value < 0) throw new QueryContractException("Array length cannot be negative.");
    }
}

public sealed class InBoundsQuery : IQuerySpec<InBoundsKey, Proof>
{
    public string Name => "InBounds";
    public QueryResult<Proof> Combine(IReadOnlyList<Proof> values) =>
        values.Any(value => value.Proven) ? QueryResult<Proof>.Known(Proof.Yes) : QueryResult<Proof>.Unknown;
}

public sealed class BranchProbabilityQuery : IQuerySpec<BranchId, double>
{
    public string Name => "BranchProbability";

    public QueryResult<double> Combine(IReadOnlyList<double> values) => values.Count switch
    {
        0 => QueryResult<double>.Unknown,
        1 => QueryResult<double>.Known(values[0]),
        _ => QueryResult<double>.Conflict
    };

    public void Validate(double value)
    {
        if (value is < 0 or > 1) throw new QueryContractException("Probability must be in [0, 1].");
    }
}

public sealed class EffectsQuery : IQuerySpec<EffectsKey, MemoryEffects>
{
    public string Name => "Effects";
    public QueryResult<MemoryEffects> Combine(IReadOnlyList<MemoryEffects> values) => values.Count switch
    {
        0 => QueryResult<MemoryEffects>.Unknown,
        1 => QueryResult<MemoryEffects>.Known(values[0]),
        _ => QueryResult<MemoryEffects>.Conflict
    };
}

public sealed class NoAliasQuery : IQuerySpec<NoAliasKey, Proof>
{
    public string Name => "NoAlias";
    public QueryResult<Proof> Combine(IReadOnlyList<Proof> values) =>
        values.Any(value => value.Proven) ? QueryResult<Proof>.Known(Proof.Yes) : QueryResult<Proof>.Unknown;
}

public sealed class CanHoistQuery : IQuerySpec<CanHoistKey, Proof>
{
    public string Name => "CanHoist";
    public QueryResult<Proof> Combine(IReadOnlyList<Proof> values) =>
        values.Any(value => value.Proven) ? QueryResult<Proof>.Known(Proof.Yes) : QueryResult<Proof>.Unknown;
}

public sealed class AlignmentQuery : IQuerySpec<AlignmentKey, int>
{
    public string Name => "Alignment";

    public QueryResult<int> Combine(IReadOnlyList<int> values)
    {
        if (values.Count == 0) return QueryResult<int>.Unknown;
        return values.All(value => value == values[0])
            ? QueryResult<int>.Known(values[0])
            : QueryResult<int>.Conflict;
    }

    public void Validate(int value)
    {
        if (value <= 0 || (value & (value - 1)) != 0)
            throw new QueryContractException("Alignment must be a positive power of two.");
    }
}
