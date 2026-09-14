using System.Diagnostics;
using Baseline.DirectServices;
using MiniCompiler.IR;
using Packages.BasicRange;
using SemanticContracts;
using SemanticQueries.Core;
using BaselineBasicRangeService = Baseline.BasicRange.BasicRangeService;

var value = new ValueId("i");
var point = new ProgramPoint("p");
var unit = new CompilationUnit().Constant(value, 5);
var key = new RangeKey(value, point);
var rangeQuery = new RangeQuery();
var direct = new BaselineBasicRangeService(unit);
var registry = new RangeRegistry([direct]);
var oneProviderPlan = new StaticPlan().Add(new BasicRangeProvider(unit));
var warmSession = NewSession(oneProviderPlan, unit);
_ = warmSession.Query(rangeQuery, key);

var rows = new List<Row>
{
    Measure("direct typed call", () => direct.TryGet(key, out _), 300_000),
    Measure("ordinary typed registry", () => registry.TryGet(key, out _), 300_000),
    Measure("semantic query warm", () => warmSession.Query(rangeQuery, key), 300_000),
    Measure("semantic query cold (BasicRange)", () => NewSession(oneProviderPlan, unit).Query(rangeQuery, key), 30_000),
    Measure("semantic query cold (1 constant provider)", ColdWithProviders(1), 20_000),
    Measure("semantic query cold (2 constant providers)", ColdWithProviders(2), 20_000),
    Measure("semantic query cold (10 constant providers)", ColdWithProviders(10), 10_000),
    Measure("semantic query cold (100 constant providers)", ColdWithProviders(100), 3_000)
};

var chainB = new ChainBQuery();
var chainC = new ChainCQuery();
var chainD = new ChainDQuery();
var chainPlan = new StaticPlan()
    .Add(new ChainAProvider(chainB))
    .Add(new ChainBProvider(chainC))
    .Add(new ChainCProvider(chainD))
    .Add(new ChainDProvider());
var chainA = new ChainAQuery();
var chainWarm = NewSession(chainPlan, unit);
_ = chainWarm.Query(chainA, "x");
rows.Add(Measure("deep A->B->C->D cold", () => NewSession(chainPlan, unit).Query(chainA, "x"), 10_000));
rows.Add(Measure("deep A->B->C->D warm", () => chainWarm.Query(chainA, "x"), 300_000));

Console.WriteLine("Toy benchmark — not a production compiler benchmark.");
Console.WriteLine("case | median ns/op | median B/op");
Console.WriteLine("--- | ---: | ---:");
foreach (var row in rows)
    Console.WriteLine($"{row.Name} | {row.NanosecondsPerOperation:F1} | {row.BytesPerOperation:F1}");

Action ColdWithProviders(int count)
{
    var plan = new StaticPlan();
    for (var index = 0; index < count; index++)
        plan.Add(new ConstantRangeProvider(new AbstractRange(0, 100)));
    return () => NewSession(plan, unit).Query(rangeQuery, key);
}

static SemanticSession NewSession(StaticPlan plan, CompilationUnit unit) =>
    new(plan, unit.Revision, () => unit.Revision);

static Row Measure(string name, Action action, int iterations, int samples = 5)
{
    for (var index = 0; index < Math.Min(iterations, 50_000); index++) action();
    var timings = new double[samples];
    var allocations = new double[samples];
    for (var sample = 0; sample < samples; sample++)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var before = GC.GetAllocatedBytesForCurrentThread();
        var stopwatch = Stopwatch.StartNew();
        for (var index = 0; index < iterations; index++) action();
        stopwatch.Stop();
        var after = GC.GetAllocatedBytesForCurrentThread();
        timings[sample] = stopwatch.Elapsed.TotalMilliseconds * 1_000_000 / iterations;
        allocations[sample] = (after - before) / (double)iterations;
    }

    Array.Sort(timings);
    Array.Sort(allocations);
    return new Row(name, timings[samples / 2], allocations[samples / 2]);
}

sealed record Row(string Name, double NanosecondsPerOperation, double BytesPerOperation);

sealed class ConstantRangeProvider(AbstractRange range)
    : IQueryProvider<RangeQuery, RangeKey, AbstractRange>
{
    public QueryResult<AbstractRange> TryGet(RangeKey key, QueryContext context) =>
        QueryResult<AbstractRange>.Known(range);
}

abstract class ChainQuery(string name) : IQuerySpec<string, int>
{
    public string Name { get; } = name;
    public QueryResult<int> Combine(IReadOnlyList<int> values) => values.Count switch
    {
        0 => QueryResult<int>.Unknown,
        1 => QueryResult<int>.Known(values[0]),
        _ => QueryResult<int>.Conflict
    };
}

sealed class ChainAQuery : ChainQuery { public ChainAQuery() : base("A") { } }
sealed class ChainBQuery : ChainQuery { public ChainBQuery() : base("B") { } }
sealed class ChainCQuery : ChainQuery { public ChainCQuery() : base("C") { } }
sealed class ChainDQuery : ChainQuery { public ChainDQuery() : base("D") { } }
sealed class ChainAProvider(ChainBQuery next) : IQueryProvider<ChainAQuery, string, int>
{
    public QueryResult<int> TryGet(string key, QueryContext context) => context.Query(next, key);
}
sealed class ChainBProvider(ChainCQuery next) : IQueryProvider<ChainBQuery, string, int>
{
    public QueryResult<int> TryGet(string key, QueryContext context) => context.Query(next, key);
}
sealed class ChainCProvider(ChainDQuery next) : IQueryProvider<ChainCQuery, string, int>
{
    public QueryResult<int> TryGet(string key, QueryContext context) => context.Query(next, key);
}
sealed class ChainDProvider : IQueryProvider<ChainDQuery, string, int>
{
    public QueryResult<int> TryGet(string key, QueryContext context) => QueryResult<int>.Known(42);
}
