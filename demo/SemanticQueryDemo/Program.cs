using MiniCompiler.IR;
using SemanticQueries.Core;
using Packages.BasicRange;
using Packages.ArraySemantics;
using Packages.BoundsBridge;
using Packages.BoundsOptimizer;
using Packages.LoopRange;
using Packages.BranchProfile;
using Packages.BranchLayout;

var i = new ValueId("i");
var a = new ArrayId("a");
var body = new ProgramPoint("loop.body");
var unit = new CompilationUnit().Array(a, 10).Loop(new(i, 0, 10, body));
var check = new BoundsCheck(a, i, body);

SemanticSession Session(bool includeLoopRange)
{
    var plan = new StaticPlan()
        .Add(new BasicRangeProvider(unit))
        .Add(new ArrayLengthProvider(unit))
        .Add(new BoundsBridgeProvider());
    if (includeLoopRange) plan.Add(new LoopRangeProvider(unit));
    return new SemanticSession(plan, unit.Revision, () => unit.Revision);
}

var optimizer = new BoundsCheckOptimizer();
Console.WriteLine("=== Without LoopRangeProvider ===");
Console.WriteLine(optimizer.Run(check, Session(false)).Removed ? "bounds check removed" : "bounds check kept");
Console.WriteLine();
Console.WriteLine("=== With LoopRangeProvider ===");
Console.WriteLine(optimizer.Run(check, Session(true)).Removed ? "bounds check removed" : "bounds check kept");

var branch = new BranchId("hot-path");
unit.ProfileBranch(branch, 0.05);
var layoutPlan = new StaticPlan().Add(new ProfileBranchProbabilityProvider(unit));
var layoutSession = new SemanticSession(layoutPlan, unit.Revision, () => unit.Revision);
Console.WriteLine();
Console.WriteLine("=== Branch probability ===");
Console.WriteLine(string.Join(" -> ", new BranchLayoutPass().Order(new Branch(branch, "cold", "hot"), layoutSession)));
