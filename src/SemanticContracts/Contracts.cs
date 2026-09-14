using MiniCompiler.IR;
using SemanticQueries.Core;
namespace SemanticContracts;

public readonly record struct AbstractRange(int Lower,int Upper)
{
    public bool Intersects(AbstractRange other)=>Math.Max(Lower,other.Lower)<=Math.Min(Upper,other.Upper);
    public AbstractRange Intersect(AbstractRange other)=>new(Math.Max(Lower,other.Lower),Math.Min(Upper,other.Upper));
}
public readonly record struct RangeKey(ValueId Value, ProgramPoint Point);
public readonly record struct LengthKey(ArrayId Array, ProgramPoint Point);
public readonly record struct InBoundsKey(ArrayId Array, ValueId Index, ProgramPoint Point);
public readonly record struct Proof(bool Proven) { public static Proof Yes => new(true); }

public sealed class RangeQuery:IQuerySpec<RangeKey,AbstractRange>
{
    public string Name=>"Range";
    public QueryResult<AbstractRange> Combine(IReadOnlyList<AbstractRange> values){
        if(values.Count==0)return QueryResult<AbstractRange>.Unknown;
        var r=values[0];
        for(var i=1;i<values.Count;i++){if(!r.Intersects(values[i]))return QueryResult<AbstractRange>.Conflict;r=r.Intersect(values[i]);}
        return QueryResult<AbstractRange>.Known(r);
    }
}
public sealed class LengthQuery:IQuerySpec<LengthKey,int>
{
    public string Name=>"Length";
    public QueryResult<int> Combine(IReadOnlyList<int> values){if(values.Count==0)return QueryResult<int>.Unknown;return values.All(v=>v==values[0])?QueryResult<int>.Known(values[0]):QueryResult<int>.Conflict;}
    public void Validate(int value){if(value<0)throw new QueryContractException("Array length cannot be negative.");}
}
public sealed class InBoundsQuery:IQuerySpec<InBoundsKey,Proof>
{
    public string Name=>"InBounds";
    public QueryResult<Proof> Combine(IReadOnlyList<Proof> values)=>values.Any(v=>v.Proven)?QueryResult<Proof>.Known(Proof.Yes):QueryResult<Proof>.Unknown;
}
public sealed class BranchProbabilityQuery:IQuerySpec<BranchId,double>
{
    public string Name=>"BranchProbability";
    public QueryResult<double> Combine(IReadOnlyList<double> values)=>values.Count switch{0=>QueryResult<double>.Unknown,1=>QueryResult<double>.Known(values[0]),_=>QueryResult<double>.Conflict};
    public void Validate(double value){if(value is <0 or >1)throw new QueryContractException("Probability must be in [0,1].");}
}
