using Reef.Core.LoweredExpressions;

namespace Reef.Core.Abseil;

public static class ExpressionAbseil
{
    public static ILoweredExpression LowerExpression(
            Expressions.IExpression expression)
    {
        return expression switch
        {
            Expressions.ValueAccessorExpression e => LowerValueAccessorExpression(e),
            Expressions.VariableDeclarationExpression e => LowerVariableDeclarationExpression(e),
            _ => throw new NotImplementedException($"{expression.GetType()}")
        };
    }

    private static ILoweredExpression LowerVariableDeclarationExpression(Expressions.VariableDeclarationExpression e)
    {
        var variableName = e.VariableDeclaration.Variable.NotNull()
            .Name.StringValue;

        if (e.VariableDeclaration.Value is null)
        {
            return new VariableDeclarationExpression(variableName, e.ValueUseful);
        }

        return new VariableDeclarationAndAssignmentExpression(
                variableName,
                LowerExpression(e.VariableDeclaration.Value),
                e.ValueUseful);
    }

    public static ILoweredExpression LowerValueAccessorExpression(
            Expressions.ValueAccessorExpression e)
    {
        return e switch
        {
            {ValueAccessor: { AccessType: Expressions.ValueAccessType.Literal, Token: StringToken {StringValue: var stringLiteral } }} => new StringConstantExpression(e.ValueUseful, stringLiteral),
            _ => throw new NotImplementedException($"e")
        };
    }
}
