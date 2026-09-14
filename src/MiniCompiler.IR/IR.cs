namespace MiniCompiler.IR;

public readonly record struct ValueId(string Name) { public override string ToString()=>Name; }
public readonly record struct ArrayId(string Name) { public override string ToString()=>Name; }
public readonly record struct ProgramPoint(string Name) { public override string ToString()=>Name; }
public readonly record struct BranchId(string Name) { public override string ToString()=>Name; }

public sealed record LoopInfo(ValueId Index, int StartInclusive, int EndExclusive, ProgramPoint BodyPoint);
public sealed record BoundsCheck(ArrayId Array, ValueId Index, ProgramPoint Point, bool Removed=false);
public sealed record LessThanCondition(ValueId Value, int Constant, ProgramPoint Point);
public sealed record Branch(BranchId Id, string TrueBlock, string FalseBlock);

public sealed class CompilationUnit
{
    private readonly Dictionary<ValueId,int> _constants = [];
    private readonly Dictionary<ArrayId,int> _arrayLengths = [];
    private readonly List<LoopInfo> _loops = [];
    private readonly Dictionary<BranchId,double> _profile = [];
    public long Revision { get; private set; } = 1;
    public IReadOnlyDictionary<ValueId,int> Constants => _constants;
    public IReadOnlyDictionary<ArrayId,int> ArrayLengths => _arrayLengths;
    public IReadOnlyList<LoopInfo> Loops => _loops;
    public IReadOnlyDictionary<BranchId,double> Profile => _profile;
    public CompilationUnit Constant(ValueId v,int x){_constants[v]=x;Revision++;return this;}
    public CompilationUnit Array(ArrayId a,int length){_arrayLengths[a]=length;Revision++;return this;}
    public CompilationUnit Loop(LoopInfo loop){_loops.Add(loop);Revision++;return this;}
    public CompilationUnit ProfileBranch(BranchId b,double probability){_profile[b]=probability;Revision++;return this;}
    public void MutateConstant(ValueId v,int x){_constants[v]=x;Revision++;}
}
