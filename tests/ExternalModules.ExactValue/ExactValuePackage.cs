using MiniCompiler.IR;
using SemanticContracts;
using SemanticQueries.Core;

namespace ExternalModules.ExactValue;

public readonly record struct ExactValueKey(ValueId Value, ProgramPoint Point);

public sealed class ExactValueQuery : IQuerySpec<ExactValueKey, int>
{
    public string Name => "ExactValue";

    public QueryResult<int> Combine(IReadOnlyList<int> values)
    {
        if (values.Count == 0) return QueryResult<int>.Unknown;
        return values.All(value => value == values[0])
            ? QueryResult<int>.Known(values[0])
            : QueryResult<int>.Conflict;
    }
}

public sealed class ExactValueAnalysis
{
    private readonly Dictionary<ExactValueKey, int> _values = [];

    public long Revision { get; private set; } = 1;

    public void Set(ValueId value, ProgramPoint point, int exactValue)
    {
        _values[new ExactValueKey(value, point)] = exactValue;
        Revision++;
    }

    public bool TryGet(ExactValueKey key, out int value) => _values.TryGetValue(key, out value);
}

public sealed class ExactValueProvider(ExactValueAnalysis analysis)
    : IQueryProvider<ExactValueQuery, ExactValueKey, int>, ISemanticRevisionSource
{
    public long SemanticRevision => analysis.Revision;

    public QueryResult<int> TryGet(ExactValueKey key, QueryContext context) =>
        analysis.TryGet(key, out var value)
            ? QueryResult<int>.Known(value)
            : QueryResult<int>.Unknown;
}

public sealed class RangeFromExactValueProvider
    : IQueryProvider<RangeQuery, RangeKey, AbstractRange>, IStableQueryProvider
{
    private static readonly ExactValueQuery ExactValue = new();

    public QueryResult<AbstractRange> TryGet(RangeKey key, QueryContext context)
    {
        var exact = context.Query(ExactValue, new ExactValueKey(key.Value, key.Point));
        return exact.Status switch
        {
            QueryStatus.Known => QueryResult<AbstractRange>.Known(new AbstractRange(exact.Value, exact.Value)),
            QueryStatus.Conflict => QueryResult<AbstractRange>.Conflict,
            _ => QueryResult<AbstractRange>.Unknown
        };
    }
}

public sealed class ExternalExactValuePackage
{
    private readonly ExactValueAnalysis _analysis = new();
    private readonly IQueryProviderRegistration[] _providers;

    public ExternalExactValuePackage()
    {
        _providers = [new ExactValueProvider(_analysis), new RangeFromExactValueProvider()];
    }

    public IReadOnlyList<IQueryProviderRegistration> Providers => _providers;

    public void Set(string valueName, string pointName, int value) =>
        _analysis.Set(new ValueId(valueName), new ProgramPoint(pointName), value);
}
