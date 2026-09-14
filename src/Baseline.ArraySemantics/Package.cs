using Baseline.DirectServices;
using MiniCompiler.IR;
using SemanticContracts;

namespace Baseline.ArraySemantics;

public sealed class ArrayLengthService(CompilationUnit unit) : ILengthService
{
    public bool TryGet(LengthKey key, out int length) => unit.TryGetArrayLength(key.Array, out length);
}
