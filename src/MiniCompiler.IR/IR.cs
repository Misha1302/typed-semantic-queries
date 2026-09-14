using System.Collections.ObjectModel;

namespace MiniCompiler.IR;

public readonly record struct ValueId(string Name) { public override string ToString() => Name; }
public readonly record struct ArrayId(string Name) { public override string ToString() => Name; }
public readonly record struct ProgramPoint(string Name) { public override string ToString() => Name; }
public readonly record struct BranchId(string Name) { public override string ToString() => Name; }
public readonly record struct MemoryLocation(string Name) { public override string ToString() => Name; }
public enum BinaryOperator { Add, Subtract, Multiply }

public sealed record LoopInfo(ValueId Index, int StartInclusive, int EndExclusive, ProgramPoint BodyPoint);
public sealed record BoundsCheck(ArrayId Array, ValueId Index, ProgramPoint Point, bool Removed = false);
public sealed record LessThanCondition(ValueId Value, int Constant, ProgramPoint Point);
public sealed record Branch(BranchId Id, string TrueBlock, string FalseBlock);
public sealed record LoadOperation(MemoryLocation Location, ProgramPoint LoopPoint, bool Hoisted = false);
public sealed record BinaryExpression(ValueId Left, ValueId Right, BinaryOperator Operator);

public sealed class CompilationUnit
{
    private readonly Dictionary<ValueId, int> _constants = [];
    private readonly Dictionary<ValueId, BinaryExpression> _binaryExpressions = [];
    private readonly Dictionary<ArrayId, int> _arrayLengths = [];
    private readonly List<LoopInfo> _loops = [];
    private readonly Dictionary<BranchId, double> _profile = [];
    private readonly Dictionary<ProgramPoint, ReadOnlyCollection<MemoryLocation>> _loopWrites = [];
    private readonly HashSet<(MemoryLocation Left, MemoryLocation Right, ProgramPoint Point)> _noAlias = [];
    private readonly Dictionary<(ValueId Value, ProgramPoint Point), int> _alignments = [];

    public long Revision { get; private set; } = 1;

    public CompilationUnit Constant(ValueId value, int constant)
    {
        _constants[value] = constant;
        Revision++;
        return this;
    }

    public CompilationUnit Binary(ValueId target, ValueId left, ValueId right, BinaryOperator operation)
    {
        _binaryExpressions[target] = new BinaryExpression(left, right, operation);
        Revision++;
        return this;
    }

    public CompilationUnit Array(ArrayId array, int length)
    {
        _arrayLengths[array] = length;
        Revision++;
        return this;
    }

    public CompilationUnit Loop(LoopInfo loop)
    {
        _loops.Add(loop);
        Revision++;
        return this;
    }

    public CompilationUnit ProfileBranch(BranchId branch, double probability)
    {
        _profile[branch] = probability;
        Revision++;
        return this;
    }

    public CompilationUnit LoopWrites(ProgramPoint loopPoint, params MemoryLocation[] writes)
    {
        _loopWrites[loopPoint] = System.Array.AsReadOnly(writes.ToArray());
        Revision++;
        return this;
    }

    public CompilationUnit NoAlias(MemoryLocation left, MemoryLocation right, ProgramPoint point)
    {
        _noAlias.Add((left, right, point));
        _noAlias.Add((right, left, point));
        Revision++;
        return this;
    }

    public CompilationUnit Alignment(ValueId value, ProgramPoint point, int bytes)
    {
        _alignments[(value, point)] = bytes;
        Revision++;
        return this;
    }

    public void MutateConstant(ValueId value, int constant)
    {
        _constants[value] = constant;
        Revision++;
    }

    public bool TryGetConstant(ValueId value, out int constant) => _constants.TryGetValue(value, out constant);
    public bool TryGetBinary(ValueId value, out BinaryExpression expression) => _binaryExpressions.TryGetValue(value, out expression!);
    public bool TryGetArrayLength(ArrayId array, out int length) => _arrayLengths.TryGetValue(array, out length);
    public bool TryGetBranchProbability(BranchId branch, out double probability) => _profile.TryGetValue(branch, out probability);
    public bool TryGetAlignment(ValueId value, ProgramPoint point, out int bytes) => _alignments.TryGetValue((value, point), out bytes);

    public bool TryGetLoop(ValueId index, ProgramPoint point, out LoopInfo loop)
    {
        var found = _loops.FirstOrDefault(candidate => candidate.Index == index && candidate.BodyPoint == point);
        if (found is null)
        {
            loop = null!;
            return false;
        }

        loop = found;
        return true;
    }

    public bool TryGetLoopWrites(ProgramPoint point, out IReadOnlyList<MemoryLocation> writes)
    {
        if (_loopWrites.TryGetValue(point, out var stored))
        {
            writes = stored;
            return true;
        }

        writes = [];
        return false;
    }

    public bool IsKnownNoAlias(MemoryLocation left, MemoryLocation right, ProgramPoint point) =>
        _noAlias.Contains((left, right, point));
}
