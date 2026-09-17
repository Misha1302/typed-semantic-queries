using MiniCompiler.IR;
using SemanticQueries.Core;

var unit = new CompilationUnit();

// Legacy independently-authored query: no explicit Identity member.
var legacyProvider = new LegacyMutableProvider();
var legacyPlan = new StaticPlan().Add(legacyProvider);
var legacySession = Session(unit, legacyPlan);
AssertEqual(1, legacySession.Query(new LegacyQuery(), "x").Value, "legacy first result");
legacyProvider.Set(2);
AssertEqual(2, legacySession.Query(new LegacyQuery(), "x").Value, "untracked mutable provider must not serve stale cache");

// Variants share one CLR-contract provider set but have distinct cache/cycle identities.
var variantProvider = new VariantProvider();
var variantPlan = new StaticPlan().Add(variantProvider);
var variantSession = Session(unit, variantPlan);
AssertEqual(11, variantSession.Query(new VariantQuery(1), "x").Value, "variant +1");
AssertEqual(11, variantSession.Query(new VariantQuery(1), "x").Value, "variant +1 cached");
AssertEqual(110, variantSession.Query(new VariantQuery(100), "x").Value, "variant +100");
AssertEqual(110, variantSession.Query(new VariantQuery(100), "x").Value, "variant +100 cached");
AssertEqual(2, variantProvider.Calls, "two variants must share provider registration but not cache identity");

// Plan mutation after session creation invalidates cached results and provider topology.
var mutablePlan = new StaticPlan();
var planSession = Session(unit, mutablePlan);
AssertEqual(QueryStatus.Unknown, planSession.Query(new RevisionQuery(), "x").Status, "empty plan result");
mutablePlan.Add(new StableRevisionProvider());
AssertThrows<StaleSemanticSessionException>(() => planSession.Query(new RevisionQuery(), "x"), "plan mutation");
AssertEqual(1, Session(unit, mutablePlan).Query(new RevisionQuery(), "x").Value, "fresh plan session");

// Revision-tracked provider-owned state invalidates an existing session explicitly.
var revisionProvider = new RevisionProvider();
var revisionPlan = new StaticPlan().Add(revisionProvider);
var revisionSession = Session(unit, revisionPlan);
AssertEqual(1, revisionSession.Query(new RevisionQuery(), "x").Value, "revision provider first result");
revisionProvider.Set(2);
AssertThrows<StaleSemanticSessionException>(() => revisionSession.Query(new RevisionQuery(), "x"), "provider revision mutation");
AssertEqual(2, Session(unit, revisionPlan).Query(new RevisionQuery(), "x").Value, "fresh revision session");

// Explicitly stable providers retain memoization.
var counting = new CountingStableProvider();
var countingSession = Session(unit, new StaticPlan().Add(counting));
_ = countingSession.Query(new RevisionQuery(), "memo");
_ = countingSession.Query(new RevisionQuery(), "memo");
AssertEqual(1, counting.Calls, "stable provider memoization");

Console.WriteLine("SEMANTIC_ARCHITECTURE_ACCEPTANCE_PASS");
return;

static SemanticSession Session(CompilationUnit unit, StaticPlan plan) =>
    new(plan, unit.Revision, () => unit.Revision);

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
}

static void AssertThrows<TException>(Action action, string label) where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException($"{label}: expected {typeof(TException).Name}");
}

sealed class LegacyQuery : IQuerySpec<string, int>
{
    public string Name => "Legacy";
    public QueryResult<int> Combine(IReadOnlyList<int> values) => values.Count == 0
        ? QueryResult<int>.Unknown
        : QueryResult<int>.Known(values[0]);
}

sealed class LegacyMutableProvider : IQueryProvider<LegacyQuery, string, int>
{
    private int _value = 1;
    public void Set(int value) => _value = value;
    public QueryResult<int> TryGet(string key, QueryContext context) => QueryResult<int>.Known(_value);
}

sealed class VariantQuery(int offset) : IQuerySpec<string, int>
{
    public QueryIdentity Identity => QueryIdentity.For<VariantQuery>(offset);
    public string Name => "Variant";
    public QueryResult<int> Combine(IReadOnlyList<int> values) => values.Count == 0
        ? QueryResult<int>.Unknown
        : QueryResult<int>.Known(values[0] + offset);
}

sealed class VariantProvider : IQueryProvider<VariantQuery, string, int>, IStableQueryProvider
{
    public int Calls { get; private set; }

    public QueryResult<int> TryGet(string key, QueryContext context)
    {
        Calls++;
        return QueryResult<int>.Known(10);
    }
}

sealed class RevisionQuery : IQuerySpec<string, int>
{
    public string Name => "Revision";
    public QueryResult<int> Combine(IReadOnlyList<int> values) => values.Count == 0
        ? QueryResult<int>.Unknown
        : QueryResult<int>.Known(values[0]);
}

sealed class StableRevisionProvider : IQueryProvider<RevisionQuery, string, int>, IStableQueryProvider
{
    public QueryResult<int> TryGet(string key, QueryContext context) => QueryResult<int>.Known(1);
}

sealed class RevisionProvider : IQueryProvider<RevisionQuery, string, int>, ISemanticRevisionSource
{
    private int _value = 1;
    public long SemanticRevision { get; private set; } = 1;
    public void Set(int value) { _value = value; SemanticRevision++; }
    public QueryResult<int> TryGet(string key, QueryContext context) => QueryResult<int>.Known(_value);
}

sealed class CountingStableProvider : IQueryProvider<RevisionQuery, string, int>, IStableQueryProvider
{
    public int Calls { get; private set; }
    public QueryResult<int> TryGet(string key, QueryContext context)
    {
        Calls++;
        return QueryResult<int>.Known(1);
    }
}
