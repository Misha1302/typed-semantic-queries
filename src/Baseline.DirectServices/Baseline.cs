using MiniCompiler.IR; using SemanticContracts;
namespace Baseline.DirectServices;
public interface IRangeService{bool TryGet(RangeKey key,out AbstractRange range);}
public interface ILengthService{bool TryGet(LengthKey key,out int length);}
public sealed class RangeRegistry(IEnumerable<IRangeService> providers){private readonly IRangeService[] _providers=providers.ToArray();public bool TryGet(RangeKey key,out AbstractRange range){range=default;var found=false;foreach(var p in _providers){if(!p.TryGet(key,out var r))continue;if(!found){range=r;found=true;}else{if(!range.Intersects(r))return false;range=range.Intersect(r);}}return found;}}
public sealed class BaselineBoundsService(RangeRegistry ranges, ILengthService lengths){public bool InBounds(InBoundsKey key){if(!ranges.TryGet(new(key.Index,key.Point),out var r)||!lengths.TryGet(new(key.Array,key.Point),out var n))return false;return r.Lower>=0&&r.Upper<n;}}
public sealed class BasicRangeService(CompilationUnit u):IRangeService{public bool TryGet(RangeKey k,out AbstractRange r){if(u.Constants.TryGetValue(k.Value,out var x)){r=new(x,x);return true;}r=default;return false;}}
public sealed class LoopRangeService(CompilationUnit u):IRangeService{public bool TryGet(RangeKey k,out AbstractRange r){var l=u.Loops.FirstOrDefault(x=>x.Index==k.Value&&x.BodyPoint==k.Point);if(l is null){r=default;return false;}r=new(l.StartInclusive,l.EndExclusive-1);return true;}}
public sealed class ArrayLengthService(CompilationUnit u):ILengthService{public bool TryGet(LengthKey k,out int n)=>u.ArrayLengths.TryGetValue(k.Array,out n);}
