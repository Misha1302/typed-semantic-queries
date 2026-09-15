using MiniCompiler.IR;
using SemanticContracts;
using SemanticQueries.Core;

namespace Packages.Effects;

public sealed class LoopEffectsAnalysis
{
    private readonly Dictionary<ProgramPoint, IReadOnlyList<MemoryLocation>> _writes = [];

    public long Revision { get; private set; } = 1;

    public LoopEffectsAnalysis SetWrites(ProgramPoint loopPoint, params MemoryLocation[] writes)
    {
        _writes[loopPoint] = Array.AsReadOnly(writes.ToArray());
        Revision++;
        return this;
    }

    public bool TryGetWrites(ProgramPoint loopPoint, out IReadOnlyList<MemoryLocation> writes)
    {
        if (_writes.TryGetValue(loopPoint, out var stored))
        {
            writes = stored;
            return true;
        }

        writes = [];
        return false;
    }
}

public sealed class LoopEffectsProvider(LoopEffectsAnalysis analysis)
    : IQueryProvider<EffectsQuery, EffectsKey, MemoryEffects>, ISemanticRevisionSource
{
    public long SemanticRevision => analysis.Revision;

    public QueryResult<MemoryEffects> TryGet(EffectsKey key, QueryContext context) =>
        analysis.TryGetWrites(key.LoopPoint, out var writes)
            ? QueryResult<MemoryEffects>.Known(new MemoryEffects(writes))
            : QueryResult<MemoryEffects>.Unknown;
}
