using MiniCompiler.IR;
using Packages.ArraySemantics;
using Packages.BasicRange;
using Packages.BoundsBridge;
using Packages.BoundsOptimizer;
using Packages.BranchLayout;
using Packages.BranchProfile;
using Packages.Effects;
using Packages.LicmBridge;
using Packages.LoopRange;
using Packages.NoAlias;
using Packages.TinyLicm;
using SemanticContracts;
using SemanticQueries.Core;

var i = new ValueId("i");
var a = new ArrayId("a");
var body = new ProgramPoint("loop.body");
var unit = new CompilationUnit().Array(a, 10).Loop(new LoopInfo(i, 0, 10, body));
var check = new BoundsCheck(a, i, body);
var optimizer = new BoundsCheckOptimizer();

SemanticSession BoundsSession(bool includeLoopRange)
{
    var plan = new StaticPlan()
        .Add(new BasicRangeProvider(unit))
        .Add(new ArrayLengthProvider(unit))
        .Add(new BoundsBridgeProvider());
    if (includeLoopRange) plan.Add(new LoopRangeProvider(unit));
    return new SemanticSession(plan, unit.Revision, () => unit.Revision);
}

Console.WriteLine("=== Without LoopRangeProvider ===");
Console.WriteLine("for i = 0 .. 9");
Console.WriteLine("    bounds_check a[i]");
Console.WriteLine("    load a[i]");
Console.WriteLine($"Optimization: bounds check {(optimizer.Run(check, BoundsSession(false)).Removed ? "removed" : "kept")}");

Console.WriteLine();
Console.WriteLine("=== With LoopRangeProvider ===");
Console.WriteLine("for i = 0 .. 9");
Console.WriteLine("    load a[i]");
Console.WriteLine($"Optimization: bounds check {(optimizer.Run(check, BoundsSession(true)).Removed ? "removed" : "kept")}");

var branch = new BranchId("hot-path");
unit.ProfileBranch(branch, 0.05);
var layoutSession = new SemanticSession(
    new StaticPlan().Add(new ProfileBranchProbabilityProvider(unit)), unit.Revision, () => unit.Revision);
Console.WriteLine();
Console.WriteLine("=== Branch probability ===");
Console.WriteLine("Before: cold -> hot");
Console.WriteLine($"After:  {string.Join(" -> ", new BranchLayoutPass().Order(new Branch(branch, "cold", "hot"), layoutSession))}");

var conflictUnit = new CompilationUnit().Constant(i, 3);
var conflictPlan = new StaticPlan()
    .Add(new BasicRangeProvider(conflictUnit))
    .Add(new DemoLengthProvider(10))
    .Add(new DemoLengthProvider(11))
    .Add(new BoundsBridgeProvider());
var conflictSession = new SemanticSession(conflictPlan, conflictUnit.Revision, () => conflictUnit.Revision);
var length = conflictSession.Query(new LengthQuery(), new LengthKey(a, body));
Console.WriteLine();
Console.WriteLine("=== Conflicting Length providers ===");
Console.WriteLine("Length A = 10");
Console.WriteLine("Length B = 11");
Console.WriteLine($"Result: {length.Status}");
Console.WriteLine($"Bounds check: {(optimizer.Run(check, conflictSession).Removed ? "removed" : "kept")}");

var load = new MemoryLocation("p");
var write = new MemoryLocation("q");
var licmUnit = new CompilationUnit().LoopWrites(body, write).NoAlias(load, write, body);
var licmPlan = new StaticPlan()
    .Add(new LoopEffectsProvider(licmUnit))
    .Add(new ExplicitNoAliasProvider(licmUnit))
    .Add(new CanHoistBridgeProvider());
var licmSession = new SemanticSession(licmPlan, licmUnit.Revision, () => licmUnit.Revision);
Console.WriteLine();
Console.WriteLine("=== Tiny LICM ===");
Console.WriteLine(new TinyLicmPass().Run(new LoadOperation(load, body), licmSession).Hoisted
    ? "load p: hoisted"
    : "load p: kept in loop");

sealed class DemoLengthProvider(int length) : IQueryProvider<LengthQuery, LengthKey, int>
{
    public QueryResult<int> TryGet(LengthKey key, QueryContext context) => QueryResult<int>.Known(length);
}
