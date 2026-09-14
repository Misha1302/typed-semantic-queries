using System.Reflection;
using Baseline.DirectServices;
using MiniCompiler.IR;
using Packages.ArraySemantics;
using Packages.BasicRange;
using Packages.BoundsBridge;
using Packages.BoundsOptimizer;
using Packages.BranchLayout;
using Packages.BranchProfile;
using Packages.BranchSimplifier;
using Packages.Effects;
using Packages.LicmBridge;
using Packages.LoopRange;
using Packages.NoAlias;
using Packages.TinyLicm;
using SemanticContracts;
using SemanticQueries.Core;
using Xunit;
using BaselineAlignmentService = Baseline.Alignment.ExplicitAlignmentService;
using BaselineArrayLengthService = Baseline.ArraySemantics.ArrayLengthService;
using BaselineBasicRangeService = Baseline.BasicRange.BasicRangeService;
using BaselineBoundsBridgeService = Baseline.BoundsBridge.BaselineBoundsBridge;
using BaselineLoopRangeService = Baseline.LoopRange.LoopRangeService;
using SemanticAlignmentProvider = Packages.Alignment.ExplicitAlignmentProvider;

namespace SemanticQueries.Tests;

public class Tests
{
    private static readonly ValueId I = new("i");
    private static readonly ValueId X = new("x");
    private static readonly ValueId Y = new("y");
    private static readonly ArrayId A = new("a");
    private static readonly ProgramPoint P = new("body");

    private static SemanticSession Session(CompilationUnit unit, StaticPlan plan) =>
        new(plan, unit.Revision, () => unit.Revision);

    [Fact]
    public void Range_Constant_IsExact()
    {
        var unit = new CompilationUnit().Constant(I, 5);
        var result = Session(unit, new StaticPlan().Add(new BasicRangeProvider(unit)))
            .Query(new RangeQuery(), new RangeKey(I, P));
        Assert.Equal(new AbstractRange(5, 5), result.Value);
    }

    [Fact]
    public void Range_SimpleArithmetic_IsExact()
    {
        var sum = new ValueId("sum");
        var product = new ValueId("product");
        var unit = new CompilationUnit()
            .Constant(X, 4)
            .Constant(Y, 3)
            .Binary(sum, X, Y, BinaryOperator.Add)
            .Binary(product, sum, Y, BinaryOperator.Multiply);
        var session = Session(unit, new StaticPlan().Add(new BasicRangeProvider(unit)));
        Assert.Equal(new AbstractRange(7, 7), session.Query(new RangeQuery(), new RangeKey(sum, P)).Value);
        Assert.Equal(new AbstractRange(21, 21), session.Query(new RangeQuery(), new RangeKey(product, P)).Value);
    }

    [Fact]
    public void Range_MultipleSoundProviders_AreIntersected()
    {
        var unit = new CompilationUnit();
        var plan = new StaticPlan()
            .Add(new ConstRangeProvider(new AbstractRange(0, 100)))
            .Add(new ConstRangeProvider(new AbstractRange(20, 50)));
        var result = Session(unit, plan).Query(new RangeQuery(), new RangeKey(I, P));
        Assert.Equal(new AbstractRange(20, 50), result.Value);
    }

    [Fact]
    public void Range_DisjointProviders_FailClosedOrConflict()
    {
        var unit = new CompilationUnit();
        var plan = new StaticPlan()
            .Add(new ConstRangeProvider(new AbstractRange(0, 10)))
            .Add(new ConstRangeProvider(new AbstractRange(20, 30)));
        Assert.Equal(QueryStatus.Conflict, Session(unit, plan).Query(new RangeQuery(), new RangeKey(I, P)).Status);
    }

    [Fact]
    public void Length_EqualProviders_Agree()
    {
        var unit = new CompilationUnit();
        var plan = new StaticPlan().Add(new ConstLengthProvider(10)).Add(new ConstLengthProvider(10));
        Assert.Equal(10, Session(unit, plan).Query(new LengthQuery(), new LengthKey(A, P)).Value);
    }

    [Fact]
    public void Length_DifferentProviders_ReturnConflict()
    {
        var unit = new CompilationUnit();
        var plan = new StaticPlan().Add(new ConstLengthProvider(10)).Add(new ConstLengthProvider(11));
        Assert.Equal(QueryStatus.Conflict, Session(unit, plan).Query(new LengthQuery(), new LengthKey(A, P)).Status);
    }

    [Fact]
    public void InBounds_RangeAndLength_ProvesAccess()
    {
        var unit = new CompilationUnit().Constant(I, 3).Array(A, 10);
        var plan = new StaticPlan()
            .Add(new BasicRangeProvider(unit))
            .Add(new ArrayLengthProvider(unit))
            .Add(new BoundsBridgeProvider());
        Assert.Equal(QueryStatus.Known, Session(unit, plan).Query(new InBoundsQuery(), new InBoundsKey(A, I, P)).Status);
    }

    [Fact]
    public void InBounds_UnknownRange_KeepsCheck()
    {
        var unit = new CompilationUnit().Array(A, 10);
        var plan = new StaticPlan().Add(new ArrayLengthProvider(unit)).Add(new BoundsBridgeProvider());
        Assert.False(new BoundsCheckOptimizer().Run(new BoundsCheck(A, I, P), Session(unit, plan)).Removed);
    }

    [Fact]
    public void BoundsOptimizer_RemovesProvenCheck()
    {
        var unit = new CompilationUnit().Constant(I, 3).Array(A, 10);
        var plan = new StaticPlan()
            .Add(new BasicRangeProvider(unit))
            .Add(new ArrayLengthProvider(unit))
            .Add(new BoundsBridgeProvider());
        Assert.True(new BoundsCheckOptimizer().Run(new BoundsCheck(A, I, P), Session(unit, plan)).Removed);
    }

    [Fact]
    public void BoundsOptimizer_KeepsUnknownCheck()
    {
        var unit = new CompilationUnit().Array(A, 10);
        var plan = new StaticPlan().Add(new ArrayLengthProvider(unit)).Add(new BoundsBridgeProvider());
        Assert.False(new BoundsCheckOptimizer().Run(new BoundsCheck(A, I, P), Session(unit, plan)).Removed);
    }

    [Fact]
    public void IndependentBridge_WorksWithoutProducerChanges()
    {
        var unit = new CompilationUnit().Constant(I, 2).Array(A, 4);
        var plan = new StaticPlan()
            .Add(new BasicRangeProvider(unit))
            .Add(new ArrayLengthProvider(unit))
            .Add(new BoundsBridgeProvider());
        var proof = Session(unit, plan).Query(new InBoundsQuery(), new InBoundsKey(A, I, P));
        Assert.Equal(QueryStatus.Known, proof.Status);
        Assert.DoesNotContain(References(typeof(BoundsBridgeProvider)), name =>
            name is "Packages.BasicRange" or "Packages.ArraySemantics" or "Packages.LoopRange");
    }

    [Fact]
    public void BetterRangeProvider_ImprovesExistingBoundsOptimizer()
    {
        var unit = LoopUnit();
        var check = new BoundsCheck(A, I, P);
        var optimizer = new BoundsCheckOptimizer();
        Assert.False(optimizer.Run(check, Session(unit, BoundsPlan(unit, includeLoopRange: false))).Removed);
        Assert.True(optimizer.Run(check, Session(unit, BoundsPlan(unit, includeLoopRange: true))).Removed);
    }

    [Fact]
    public void BetterRangeProvider_AlsoImprovesBranchSimplifier()
    {
        var unit = new CompilationUnit().Loop(new LoopInfo(I, 0, 10, P));
        var session = Session(unit, new StaticPlan().Add(new LoopRangeProvider(unit)));
        Assert.True(new BranchSimplificationPass().IsAlwaysTrue(new LessThanCondition(I, 10, P), session));
    }

    [Fact]
    public void InductionVariableProvider_InfersLoopRange()
    {
        var unit = new CompilationUnit().Loop(new LoopInfo(I, 0, 10, P));
        var result = Session(unit, new StaticPlan().Add(new LoopRangeProvider(unit)))
            .Query(new RangeQuery(), new RangeKey(I, P));
        Assert.Equal(new AbstractRange(0, 9), result.Value);
    }

    [Fact]
    public void InductionVariableProvider_EnablesBoundsCheckElimination()
    {
        var unit = LoopUnit();
        Assert.True(new BoundsCheckOptimizer()
            .Run(new BoundsCheck(A, I, P), Session(unit, BoundsPlan(unit, includeLoopRange: true))).Removed);
    }

    [Fact]
    public void ProviderRegistrationOrder_DoesNotChangeResult()
    {
        var unit = LoopUnit();
        var orders = new[]
        {
            new[] { "basic", "length", "loop", "bridge" },
            new[] { "loop", "bridge", "basic", "length" },
            new[] { "bridge", "length", "loop", "basic" }
        };
        var results = orders.Select(order =>
            Session(unit, OrderedBoundsPlan(unit, order)).Query(new InBoundsQuery(), new InBoundsKey(A, I, P))).ToArray();
        Assert.All(results, result => Assert.Equal(results[0], result));
    }

    [Fact]
    public void ProviderRegistrationRandomPermutations_DoNotChangeResult()
    {
        var unit = LoopUnit();
        foreach (var order in Permutations(new[] { "basic", "loop", "length", "bridge" }))
        {
            var result = Session(unit, OrderedBoundsPlan(unit, order))
                .Query(new InBoundsQuery(), new InBoundsKey(A, I, P));
            Assert.Equal(QueryStatus.Known, result.Status);
        }
    }

    [Fact]
    public void SemanticSession_MemoizesRepeatedQuery()
    {
        var unit = new CompilationUnit().Constant(I, 5);
        var provider = new BasicRangeProvider(unit);
        var session = Session(unit, new StaticPlan().Add(provider));
        for (var iteration = 0; iteration < 100; iteration++)
            session.Query(new RangeQuery(), new RangeKey(I, P));
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public void IRMutation_ChangesRevision()
    {
        var unit = new CompilationUnit().Constant(I, 5);
        var before = unit.Revision;
        unit.MutateConstant(I, 105);
        Assert.True(unit.Revision > before);
    }

    [Fact]
    public void OldSemanticSession_CannotServeNewRevision()
    {
        var unit = new CompilationUnit().Constant(I, 5);
        var session = Session(unit, new StaticPlan().Add(new BasicRangeProvider(unit)));
        _ = session.Query(new RangeQuery(), new RangeKey(I, P));
        unit.MutateConstant(I, 105);
        Assert.Throws<StaleSemanticSessionException>(() => session.Query(new RangeQuery(), new RangeKey(I, P)));
    }

    [Fact]
    public void Mutation_DoesNotReuseOldRange()
    {
        var unit = new CompilationUnit().Constant(I, 5);
        var plan = new StaticPlan().Add(new BasicRangeProvider(unit));
        var oldSession = Session(unit, plan);
        Assert.Equal(new AbstractRange(5, 5), oldSession.Query(new RangeQuery(), new RangeKey(I, P)).Value);
        unit.MutateConstant(I, 105);
        var newSession = Session(unit, plan);
        Assert.Equal(new AbstractRange(105, 105), newSession.Query(new RangeQuery(), new RangeKey(I, P)).Value);
    }

    [Fact]
    public void QueryCycle_IsDetected()
    {
        var unit = new CompilationUnit();
        var queryA = new CycleAQuery();
        var queryB = new CycleBQuery();
        var plan = new StaticPlan()
            .Add(new CycleAProvider(queryB))
            .Add(new CycleBProvider(queryA));
        Assert.Throws<QueryCycleException>(() => Session(unit, plan).Query(queryA, "x"));
    }

    [Fact]
    public void LengthConflict_DoesNotRemoveBoundsCheck()
    {
        var unit = new CompilationUnit().Constant(I, 3);
        var plan = new StaticPlan()
            .Add(new BasicRangeProvider(unit))
            .Add(new ConstLengthProvider(10))
            .Add(new ConstLengthProvider(11))
            .Add(new BoundsBridgeProvider());
        Assert.False(new BoundsCheckOptimizer().Run(new BoundsCheck(A, I, P), Session(unit, plan)).Removed);
    }

    [Fact]
    public void Probability_ReordersBranchLayout()
    {
        var branch = new BranchId("b");
        var unit = new CompilationUnit().ProfileBranch(branch, 0.05);
        var session = Session(unit, new StaticPlan().Add(new ProfileBranchProbabilityProvider(unit)));
        Assert.Equal(new[] { "hot", "cold" }, new BranchLayoutPass().Order(new Branch(branch, "cold", "hot"), session));
    }

    [Fact]
    public void Probability_DoesNotRemoveBoundsCheck()
    {
        var branch = new BranchId("b");
        var unit = new CompilationUnit().Array(A, 10).ProfileBranch(branch, 0.999999);
        var plan = new StaticPlan()
            .Add(new ProfileBranchProbabilityProvider(unit))
            .Add(new ArrayLengthProvider(unit))
            .Add(new BoundsBridgeProvider());
        Assert.False(new BoundsCheckOptimizer().Run(new BoundsCheck(A, I, P), Session(unit, plan)).Removed);
    }

    [Fact]
    public void UnknownSemanticKnowledge_FailsClosed()
    {
        var unit = new CompilationUnit();
        var plan = new StaticPlan().Add(new BoundsBridgeProvider());
        var proof = Session(unit, plan).Query(new InBoundsQuery(), new InBoundsKey(A, I, P));
        Assert.Equal(QueryStatus.Unknown, proof.Status);
        Assert.False(new BoundsCheckOptimizer().Run(new BoundsCheck(A, I, P), Session(unit, plan)).Removed);
    }

    [Fact]
    public void Consumer_DoesNotReferenceConcreteProvider()
    {
        var forbidden = new[] { "Packages.BasicRange", "Packages.ArraySemantics", "Packages.LoopRange" };
        Assert.DoesNotContain(References(typeof(BoundsCheckOptimizer)), forbidden.Contains);
        Assert.DoesNotContain(References(typeof(BranchSimplificationPass)), forbidden.Contains);
    }

    [Fact]
    public void Bridge_DoesNotReferenceConcreteProducer()
    {
        var forbidden = new[] { "Packages.BasicRange", "Packages.ArraySemantics", "Packages.LoopRange" };
        Assert.DoesNotContain(References(typeof(BoundsBridgeProvider)), forbidden.Contains);
    }

    [Fact]
    public void MalformedProbability_IsRejected()
    {
        var unit = new CompilationUnit();
        var plan = new StaticPlan().Add(new BadProbabilityProvider());
        Assert.Throws<QueryContractException>(() =>
            Session(unit, plan).Query(new BranchProbabilityQuery(), new BranchId("b")));
    }

    [Fact]
    public void BranchProbability_MultipleProviders_Conflict()
    {
        var unit = new CompilationUnit();
        var plan = new StaticPlan()
            .Add(new ConstProbabilityProvider(0.1))
            .Add(new ConstProbabilityProvider(0.9));
        Assert.Equal(QueryStatus.Conflict,
            Session(unit, plan).Query(new BranchProbabilityQuery(), new BranchId("b")).Status);
    }

    [Fact]
    public void Metamorphic_BetterProviderChangesOnlyOptimizationResult()
    {
        var unit = LoopUnit();
        var check = new BoundsCheck(A, I, P);
        var optimizer = new BoundsCheckOptimizer();
        var without = optimizer.Run(check, Session(unit, BoundsPlan(unit, includeLoopRange: false)));
        var with = optimizer.Run(check, Session(unit, BoundsPlan(unit, includeLoopRange: true)));
        Assert.False(without.Removed);
        Assert.True(with.Removed);
        Assert.Equal(without with { Removed = true }, with);
    }

    [Fact]
    public void Metamorphic_ProviderOrderKeepsOptimizedIR()
    {
        var unit = LoopUnit();
        var check = new BoundsCheck(A, I, P);
        var optimizer = new BoundsCheckOptimizer();
        var outputs = Permutations(new[] { "basic", "loop", "length", "bridge" })
            .Select(order => optimizer.Run(check, Session(unit, OrderedBoundsPlan(unit, order))))
            .ToArray();
        Assert.All(outputs, output => Assert.Equal(outputs[0], output));
    }

    [Fact]
    public void TinyLicm_HoistsWithEffectsAndNoAlias()
    {
        var load = new MemoryLocation("p");
        var write = new MemoryLocation("q");
        var unit = new CompilationUnit().LoopWrites(P, write).NoAlias(load, write, P);
        var plan = LicmPlan(unit);
        Assert.True(new TinyLicmPass().Run(new LoadOperation(load, P), Session(unit, plan)).Hoisted);
    }

    [Fact]
    public void TinyLicm_KeepsWhenAliasUnknown()
    {
        var load = new MemoryLocation("p");
        var write = new MemoryLocation("q");
        var unit = new CompilationUnit().LoopWrites(P, write);
        Assert.False(new TinyLicmPass().Run(new LoadOperation(load, P), Session(unit, LicmPlan(unit))).Hoisted);
    }

    [Fact]
    public void TinyLicm_KeepsWhenLoopWritesSameLocation()
    {
        var load = new MemoryLocation("p");
        var unit = new CompilationUnit().LoopWrites(P, load);
        Assert.False(new TinyLicmPass().Run(new LoadOperation(load, P), Session(unit, LicmPlan(unit))).Hoisted);
    }

    [Fact]
    public void ConventionalBaseline_AlsoAcceptsIndependentLoopRange()
    {
        var unit = LoopUnit();
        var length = new BaselineArrayLengthService(unit);
        var basicBridge = new BaselineBoundsBridgeService(
            new RangeRegistry([new BaselineBasicRangeService(unit)]), length);
        var extendedBridge = new BaselineBoundsBridgeService(
            new RangeRegistry([new BaselineBasicRangeService(unit), new BaselineLoopRangeService(unit)]), length);
        var check = new BoundsCheck(A, I, P);
        Assert.False(new BaselineBoundsOptimizer(basicBridge).Run(check).Removed);
        Assert.True(new BaselineBoundsOptimizer(extendedBridge).Run(check).Removed);
    }

    [Fact]
    public void ConventionalBaseline_BridgeDoesNotReferenceConcreteProviders()
    {
        var forbidden = new[] { "Baseline.BasicRange", "Baseline.LoopRange", "Baseline.ArraySemantics" };
        Assert.DoesNotContain(References(typeof(BaselineBoundsBridgeService)), forbidden.Contains);
    }

    [Fact]
    public void Alignment_NewQuery_WorksInBothArchitectures()
    {
        var unit = new CompilationUnit().Alignment(I, P, 16);
        var semantic = Session(unit, new StaticPlan().Add(new SemanticAlignmentProvider(unit)))
            .Query(new AlignmentQuery(), new AlignmentKey(I, P));
        var baseline = new BaselineAlignmentService(unit);
        Assert.Equal(16, semantic.Value);
        Assert.True(baseline.TryGet(new AlignmentKey(I, P), out var bytes));
        Assert.Equal(16, bytes);
    }

    [Fact]
    public void QueryTypesWithSameKeyAndValue_DoNotShareProviders()
    {
        var unit = new CompilationUnit();
        var plan = new StaticPlan().Add(new AlphaProvider()).Add(new BetaProvider());
        var session = Session(unit, plan);
        Assert.Equal(1, session.Query(new AlphaQuery(), "key").Value);
        Assert.Equal(2, session.Query(new BetaQuery(), "key").Value);
    }

    [Fact]
    public void DeepNestedQueryChain_ReturnsValue()
    {
        var unit = new CompilationUnit();
        var b = new ChainBQuery();
        var c = new ChainCQuery();
        var d = new ChainDQuery();
        var plan = new StaticPlan()
            .Add(new ChainAProvider(b))
            .Add(new ChainBProvider(c))
            .Add(new ChainCProvider(d))
            .Add(new ChainDProvider());
        Assert.Equal(42, Session(unit, plan).Query(new ChainAQuery(), "x").Value);
    }

    [Fact]
    public void IRState_IsNotExposedAsMutableCollections()
    {
        var publicProperties = typeof(CompilationUnit).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        Assert.Equal(new[] { "Revision" }, publicProperties.Select(property => property.Name));
    }

    private static CompilationUnit LoopUnit() =>
        new CompilationUnit().Array(A, 10).Loop(new LoopInfo(I, 0, 10, P));

    private static StaticPlan BoundsPlan(CompilationUnit unit, bool includeLoopRange)
    {
        var plan = new StaticPlan()
            .Add(new BasicRangeProvider(unit))
            .Add(new ArrayLengthProvider(unit))
            .Add(new BoundsBridgeProvider());
        if (includeLoopRange) plan.Add(new LoopRangeProvider(unit));
        return plan;
    }

    private static StaticPlan OrderedBoundsPlan(CompilationUnit unit, IEnumerable<string> order)
    {
        var plan = new StaticPlan();
        foreach (var item in order)
        {
            plan.Add(item switch
            {
                "basic" => new BasicRangeProvider(unit),
                "loop" => new LoopRangeProvider(unit),
                "length" => new ArrayLengthProvider(unit),
                "bridge" => new BoundsBridgeProvider(),
                _ => throw new ArgumentOutOfRangeException(nameof(order), item, null)
            });
        }

        return plan;
    }

    private static StaticPlan LicmPlan(CompilationUnit unit) =>
        new StaticPlan()
            .Add(new LoopEffectsProvider(unit))
            .Add(new ExplicitNoAliasProvider(unit))
            .Add(new CanHoistBridgeProvider());

    private static IEnumerable<string[]> Permutations(string[] items)
    {
        if (items.Length == 1)
        {
            yield return [items[0]];
            yield break;
        }

        for (var index = 0; index < items.Length; index++)
        {
            var head = items[index];
            var tail = items.Where((_, candidate) => candidate != index).ToArray();
            foreach (var permutation in Permutations(tail))
                yield return [head, .. permutation];
        }
    }

    private static string[] References(Type type) =>
        type.Assembly.GetReferencedAssemblies().Select(reference => reference.Name!).ToArray();

    private sealed class ConstRangeProvider(AbstractRange range)
        : IQueryProvider<RangeQuery, RangeKey, AbstractRange>
    {
        public QueryResult<AbstractRange> TryGet(RangeKey key, QueryContext context) =>
            QueryResult<AbstractRange>.Known(range);
    }

    private sealed class ConstLengthProvider(int length)
        : IQueryProvider<LengthQuery, LengthKey, int>
    {
        public QueryResult<int> TryGet(LengthKey key, QueryContext context) => QueryResult<int>.Known(length);
    }

    private sealed class BadProbabilityProvider
        : IQueryProvider<BranchProbabilityQuery, BranchId, double>
    {
        public QueryResult<double> TryGet(BranchId key, QueryContext context) => QueryResult<double>.Known(2.4);
    }

    private sealed class ConstProbabilityProvider(double probability)
        : IQueryProvider<BranchProbabilityQuery, BranchId, double>
    {
        public QueryResult<double> TryGet(BranchId key, QueryContext context) =>
            QueryResult<double>.Known(probability);
    }

    private abstract class SingleIntQuery(string name) : IQuerySpec<string, int>
    {
        public string Name { get; } = name;
        public QueryResult<int> Combine(IReadOnlyList<int> values) => values.Count switch
        {
            0 => QueryResult<int>.Unknown,
            1 => QueryResult<int>.Known(values[0]),
            _ => QueryResult<int>.Conflict
        };
    }

    private sealed class CycleAQuery : SingleIntQuery { public CycleAQuery() : base("CycleA") { } }
    private sealed class CycleBQuery : SingleIntQuery { public CycleBQuery() : base("CycleB") { } }
    private sealed class CycleAProvider(CycleBQuery b) : IQueryProvider<CycleAQuery, string, int>
    {
        public QueryResult<int> TryGet(string key, QueryContext context) => context.Query(b, key);
    }
    private sealed class CycleBProvider(CycleAQuery a) : IQueryProvider<CycleBQuery, string, int>
    {
        public QueryResult<int> TryGet(string key, QueryContext context) => context.Query(a, key);
    }

    private sealed class AlphaQuery : SingleIntQuery { public AlphaQuery() : base("Alpha") { } }
    private sealed class BetaQuery : SingleIntQuery { public BetaQuery() : base("Beta") { } }
    private sealed class AlphaProvider : IQueryProvider<AlphaQuery, string, int>
    {
        public QueryResult<int> TryGet(string key, QueryContext context) => QueryResult<int>.Known(1);
    }
    private sealed class BetaProvider : IQueryProvider<BetaQuery, string, int>
    {
        public QueryResult<int> TryGet(string key, QueryContext context) => QueryResult<int>.Known(2);
    }

    private sealed class ChainAQuery : SingleIntQuery { public ChainAQuery() : base("A") { } }
    private sealed class ChainBQuery : SingleIntQuery { public ChainBQuery() : base("B") { } }
    private sealed class ChainCQuery : SingleIntQuery { public ChainCQuery() : base("C") { } }
    private sealed class ChainDQuery : SingleIntQuery { public ChainDQuery() : base("D") { } }
    private sealed class ChainAProvider(ChainBQuery next) : IQueryProvider<ChainAQuery, string, int>
    {
        public QueryResult<int> TryGet(string key, QueryContext context) => context.Query(next, key);
    }
    private sealed class ChainBProvider(ChainCQuery next) : IQueryProvider<ChainBQuery, string, int>
    {
        public QueryResult<int> TryGet(string key, QueryContext context) => context.Query(next, key);
    }
    private sealed class ChainCProvider(ChainDQuery next) : IQueryProvider<ChainCQuery, string, int>
    {
        public QueryResult<int> TryGet(string key, QueryContext context) => context.Query(next, key);
    }
    private sealed class ChainDProvider : IQueryProvider<ChainDQuery, string, int>
    {
        public QueryResult<int> TryGet(string key, QueryContext context) => QueryResult<int>.Known(42);
    }
}
