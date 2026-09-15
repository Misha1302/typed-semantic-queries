namespace SemanticQueries.Core;

public enum QueryStatus
{
    Unknown,
    Known,
    Conflict
}

public readonly record struct QueryResult<T>(QueryStatus Status, T? Value = default)
{
    public static QueryResult<T> Unknown => new(QueryStatus.Unknown);
    public static QueryResult<T> Conflict => new(QueryStatus.Conflict);
    public static QueryResult<T> Known(T value) => new(QueryStatus.Known, value);
}

public readonly record struct QueryIdentity(Type ContractType, object? Variant = null)
{
    public static QueryIdentity For<TQuery>(object? variant = null) => new(typeof(TQuery), variant);
}

public interface IQuerySpec<TKey, TValue> where TKey : notnull
{
    QueryIdentity Identity => new(GetType());
    string Name { get; }
    QueryResult<TValue> Combine(IReadOnlyList<TValue> values);
    void Validate(TValue value) { }
}

public interface IQueryProviderRegistration
{
    Type QueryType { get; }
}

public interface ISemanticRevisionSource
{
    long SemanticRevision { get; }
}

public interface IStableQueryProvider { }

public interface IQueryProvider<TKey, TValue> where TKey : notnull
{
    QueryResult<TValue> TryGet(TKey key, QueryContext context);
}

public interface IQueryProvider<TQuery, TKey, TValue>
    : IQueryProvider<TKey, TValue>, IQueryProviderRegistration
    where TKey : notnull
    where TQuery : IQuerySpec<TKey, TValue>
{
    Type IQueryProviderRegistration.QueryType => typeof(TQuery);
}

public sealed class StaticPlan
{
    private readonly Dictionary<Type, List<object>> _providers = new();
    private readonly List<ISemanticRevisionSource> _revisionSources = [];
    private bool _hasUntrackedProviders;

    public long Revision { get; private set; } = 1;

    public StaticPlan Add(IQueryProviderRegistration provider)
    {
        if (!_providers.TryGetValue(provider.QueryType, out var providers))
        {
            providers = [];
            _providers[provider.QueryType] = providers;
        }

        providers.Add(provider);
        if (provider is ISemanticRevisionSource revisionSource)
        {
            if (!_revisionSources.Any(existing => ReferenceEquals(existing, revisionSource)))
                _revisionSources.Add(revisionSource);
        }
        else if (provider is not IStableQueryProvider)
        {
            _hasUntrackedProviders = true;
        }

        Revision++;
        return this;
    }

    internal IReadOnlyList<object> Providers(Type queryType) =>
        _providers.TryGetValue(queryType, out var providers) ? providers : [];

    internal IReadOnlyList<ISemanticRevisionSource> RevisionSources => _revisionSources;
    internal bool CacheSafe => !_hasUntrackedProviders;
}

public sealed class QueryCycleException(string message) : InvalidOperationException(message);
public sealed class StaleSemanticSessionException(string message) : InvalidOperationException(message);
public sealed class QueryContractException(string message) : InvalidOperationException(message);

public sealed class QueryContext(SemanticSession session)
{
    public QueryResult<TValue> Query<TKey, TValue>(IQuerySpec<TKey, TValue> spec, TKey key)
        where TKey : notnull => session.Query(spec, key);
}

public sealed class SemanticSession
{
    private readonly StaticPlan _plan;
    private readonly long _revision;
    private readonly Func<long> _currentRevision;
    private readonly Dictionary<(QueryIdentity Query, object Key), object> _cache = new();
    private readonly HashSet<(QueryIdentity Query, object Key)> _active = [];
    private readonly (ISemanticRevisionSource Source, long Revision)[] _providerRevisions;
    private readonly long _planRevision;
    private readonly bool _cacheSafe;

    public SemanticSession(StaticPlan plan, long revision, Func<long> currentRevision)
    {
        (_plan, _revision, _currentRevision) = (plan, revision, currentRevision);
        _providerRevisions = plan.RevisionSources.Select(source => (source, source.SemanticRevision)).ToArray();
        _planRevision = plan.Revision;
        _cacheSafe = plan.CacheSafe;
    }

    public QueryResult<TValue> Query<TKey, TValue>(IQuerySpec<TKey, TValue> spec, TKey key)
        where TKey : notnull
    {
        EnsureCurrentRevision();

        var queryType = spec.GetType();
        if (spec.Identity.ContractType != queryType)
            throw new QueryContractException($"Query identity {spec.Identity.ContractType} does not match runtime query type {queryType}.");

        var cacheKey = (spec.Identity, (object)key);
        if (_cacheSafe && _cache.TryGetValue(cacheKey, out var cached))
            return (QueryResult<TValue>)cached;

        if (!_active.Add(cacheKey))
            throw new QueryCycleException($"Semantic query cycle detected at {spec.Name}({key}).");

        try
        {
            var values = new List<TValue>();
            var sawConflict = false;
            var context = new QueryContext(this);

            foreach (var rawProvider in _plan.Providers(queryType))
            {
                var provider = (IQueryProvider<TKey, TValue>)rawProvider;
                var result = provider.TryGet(key, context);
                if (result.Status == QueryStatus.Conflict)
                {
                    sawConflict = true;
                }
                else if (result.Status == QueryStatus.Known)
                {
                    spec.Validate(result.Value!);
                    values.Add(result.Value!);
                }
            }

            var combined = sawConflict ? QueryResult<TValue>.Conflict : spec.Combine(values);
            if (combined.Status == QueryStatus.Known)
                spec.Validate(combined.Value!);
            if (_cacheSafe)
                _cache[cacheKey] = combined;
            return combined;
        }
        finally
        {
            _active.Remove(cacheKey);
        }
    }

    private void EnsureCurrentRevision()
    {
        if (_plan.Revision != _planRevision)
            throw new StaleSemanticSessionException(
                $"Static plan changed from revision {_planRevision} to {_plan.Revision}.");

        var current = _currentRevision();
        if (current != _revision)
            throw new StaleSemanticSessionException(
                $"Session revision {_revision} is stale; current revision is {current}.");

        foreach (var (source, revision) in _providerRevisions)
        {
            if (source.SemanticRevision != revision)
                throw new StaleSemanticSessionException(
                    $"Provider-owned semantic state changed from revision {revision} to {source.SemanticRevision}.");
        }
    }
}
