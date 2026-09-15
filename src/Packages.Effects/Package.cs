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
        ArgumentNullException.ThrowIfNull(writes);
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

public sealed class LoopEffectsProvider
    : IQueryProvider<EffectsQuery, EffectsKey, MemoryEffects>, ISemanticRevisionSource
{
    private readonly LoopEffectsAnalysis _analysis;

    public LoopEffectsProvider(LoopEffectsAnalysis analysis)
        => _analysis = analysis ?? throw new ArgumentNullException(nameof(analysis));

    public long SemanticRevision => _analysis.Revision;

    public QueryResult<MemoryEffects> TryGet(EffectsKey key, QueryContext context) =>
        _analysis.TryGetWrites(key.LoopPoint, out var writes)
            ? QueryResult<MemoryEffects>.Known(new MemoryEffects(writes))
            : QueryResult<MemoryEffects>.Unknown;
}
