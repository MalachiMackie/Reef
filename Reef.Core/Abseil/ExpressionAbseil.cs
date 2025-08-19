using Reef.Core.LoweredExpressions;

namespace Reef.Core.Abseil;

public static class ExpressionAbseil
{
    public static IEnumerable<ILoweredExpression> LowerExpression(
            Reef.Core.Expressions.IExpression expression)
    {
        return expression switch
        {
            Expressions.ValueAccessorExpression e => LowerValueAccessorExpression(e),
            _ => throw new NotImplementedException($"{expression.GetType()}")
        };
    }

    public static IEnumerable<ILoweredExpression> LowerValueAccessorExpression(
            Expressions.ValueAccessorExpression e)
    {
        return e switch
        {
            {ValueAccessor: { AccessType: Expressions.ValueAccessType.Literal, Token: StringToken {StringValue: var stringLiteral } }} => [new StringConstantExpression(e.ValueUseful, stringLiteral)],
            _ => throw new NotImplementedException($"e")
        };
    }
}
