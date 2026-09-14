using MiniCompiler.IR;
using SemanticContracts;
using SemanticQueries.Core;

namespace Packages.BasicRange;

public sealed class BasicRangeProvider(CompilationUnit unit)
    : IQueryProvider<RangeQuery, RangeKey, AbstractRange>
{
    private static readonly RangeQuery Range = new();
    public int Calls { get; private set; }

    public QueryResult<AbstractRange> TryGet(RangeKey key, QueryContext context)
    {
        Calls++;
        if (unit.TryGetConstant(key.Value, out var value))
            return QueryResult<AbstractRange>.Known(new(value, value));

        if (!unit.TryGetBinary(key.Value, out var expression))
            return QueryResult<AbstractRange>.Unknown;

        var left = context.Query(Range, new RangeKey(expression.Left, key.Point));
        var right = context.Query(Range, new RangeKey(expression.Right, key.Point));
        if (left.Status != QueryStatus.Known || right.Status != QueryStatus.Known)
            return QueryResult<AbstractRange>.Unknown;

        return QueryResult<AbstractRange>.Known(Apply(expression.Operator, left.Value!, right.Value!));
    }

    private static AbstractRange Apply(BinaryOperator operation, AbstractRange left, AbstractRange right) => operation switch
    {
        BinaryOperator.Add => new(left.Lower + right.Lower, left.Upper + right.Upper),
        BinaryOperator.Subtract => new(left.Lower - right.Upper, left.Upper - right.Lower),
        BinaryOperator.Multiply => Multiply(left, right),
        _ => throw new ArgumentOutOfRangeException(nameof(operation))
    };

    private static AbstractRange Multiply(AbstractRange left, AbstractRange right)
    {
        var candidates = new[]
        {
            left.Lower * right.Lower,
            left.Lower * right.Upper,
            left.Upper * right.Lower,
            left.Upper * right.Upper
        };
        return new AbstractRange(candidates.Min(), candidates.Max());
    }
}
