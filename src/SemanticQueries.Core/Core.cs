namespace SemanticQueries.Core;

public enum QueryStatus { Unknown, Known, Conflict }

public readonly record struct QueryResult<T>(QueryStatus Status, T? Value = default)
{
    public static QueryResult<T> Unknown => new(QueryStatus.Unknown);
    public static QueryResult<T> Conflict => new(QueryStatus.Conflict);
    public static QueryResult<T> Known(T value) => new(QueryStatus.Known, value);
}

public interface IQuerySpec<TKey, TValue> where TKey : notnull
{
    string Name { get; }
    QueryResult<TValue> Combine(IReadOnlyList<TValue> values);
    void Validate(TValue value) { }
}

public interface IQueryProvider<TKey, TValue> where TKey : notnull
{
    QueryResult<TValue> TryGet(TKey key, QueryContext context);
}

public sealed class StaticPlan
{
    private readonly Dictionary<Type, List<object>> _providers = new();
    public StaticPlan Add<TKey,TValue>(IQueryProvider<TKey,TValue> provider) where TKey:notnull
    {
        var t=typeof(IQueryProvider<TKey,TValue>);
        if(!_providers.TryGetValue(t,out var list)) _providers[t]=list=[];
        list.Add(provider); return this;
    }
    internal IReadOnlyList<object> Providers<TKey,TValue>() where TKey:notnull =>
        _providers.TryGetValue(typeof(IQueryProvider<TKey,TValue>), out var list) ? list : [];
}

public sealed class QueryCycleException(string message): InvalidOperationException(message);
public sealed class StaleSemanticSessionException(string message): InvalidOperationException(message);
public sealed class QueryContractException(string message): InvalidOperationException(message);

public sealed class QueryContext(SemanticSession session)
{
    public QueryResult<TValue> Query<TKey,TValue>(IQuerySpec<TKey,TValue> spec, TKey key) where TKey:notnull => session.Query(spec,key);
}

public sealed class SemanticSession
{
    private readonly StaticPlan _plan;
    private readonly long _revision;
    private readonly Func<long> _currentRevision;
    private readonly Dictionary<(Type Spec, object Key), object> _cache = new();
    private readonly HashSet<(Type Spec, object Key)> _active = [];
    public SemanticSession(StaticPlan plan, long revision, Func<long> currentRevision)
        => (_plan,_revision,_currentRevision)=(plan,revision,currentRevision);

    public QueryResult<TValue> Query<TKey,TValue>(IQuerySpec<TKey,TValue> spec, TKey key) where TKey:notnull
    {
        if(_currentRevision()!=_revision) throw new StaleSemanticSessionException($"Session revision {_revision} is stale; current revision is {_currentRevision()}.");
        var cacheKey=(spec.GetType(),(object)key);
        if(_cache.TryGetValue(cacheKey,out var cached)) return (QueryResult<TValue>)cached;
        if(!_active.Add(cacheKey)) throw new QueryCycleException($"Semantic query cycle detected at {spec.Name}({key}).");
        try {
            var values=new List<TValue>();
            var sawConflict=false;
            var ctx=new QueryContext(this);
            foreach(var raw in _plan.Providers<TKey,TValue>()) {
                var result=((IQueryProvider<TKey,TValue>)raw).TryGet(key,ctx);
                if(result.Status==QueryStatus.Conflict) sawConflict=true;
                else if(result.Status==QueryStatus.Known) { spec.Validate(result.Value!); values.Add(result.Value!); }
            }
            var combined=sawConflict ? QueryResult<TValue>.Conflict : spec.Combine(values);
            _cache[cacheKey]=combined; return combined;
        } finally { _active.Remove(cacheKey); }
    }
}
