using Baseline.DirectServices;
using MiniCompiler.IR;
using SemanticContracts;

namespace Baseline.Alignment;

public sealed class ExplicitAlignmentService(CompilationUnit unit) : IAlignmentService
{
    public bool TryGet(AlignmentKey key, out int bytes) => unit.TryGetAlignment(key.Value, key.Point, out bytes);
}
