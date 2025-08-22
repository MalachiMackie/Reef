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
            Expressions.BinaryOperatorExpression e => LowerBinaryOperatorExpression(e),
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

    private static ILoweredExpression LowerValueAccessorExpression(
            Expressions.ValueAccessorExpression e)
    {
        return e switch
        {
            { ValueAccessor: { AccessType: Expressions.ValueAccessType.Literal, Token: StringToken { StringValue: var stringLiteral } } } => new StringConstantExpression(e.ValueUseful, stringLiteral),
            { ValueAccessor: { AccessType: Expressions.ValueAccessType.Literal, Token: IntToken { Type: TokenType.IntLiteral, IntValue: var intValue} }} => new IntConstantExpression(e.ValueUseful, intValue),
            _ => throw new NotImplementedException($"e")
        };
    }

    private static ILoweredExpression LowerBinaryOperatorExpression(
            Expressions.BinaryOperatorExpression e)
    {
        switch (e.BinaryOperator.OperatorType)
        {
            case Expressions.BinaryOperatorType.LessThan:
                break;
            case Expressions.BinaryOperatorType.GreaterThan:
                break;
            case Expressions.BinaryOperatorType.Plus:
                break;
            case Expressions.BinaryOperatorType.Minus:
                break;
            case Expressions.BinaryOperatorType.Multiply:
                break;
            case Expressions.BinaryOperatorType.Divide:
                break;
            case Expressions.BinaryOperatorType.EqualityCheck:
                break;
            case Expressions.BinaryOperatorType.ValueAssignment:
                {
                    return LowerValueAssignment(
                            e.BinaryOperator.Left.NotNull(),
                            e.BinaryOperator.Right.NotNull(),
                            e.ValueUseful);
                }
            case Expressions.BinaryOperatorType.BooleanAnd:
                break;
            case Expressions.BinaryOperatorType.BooleanOr:
                break;
            default:
                throw new InvalidOperationException($"Invalid binary operator {e.BinaryOperator.OperatorType}");
        }

        throw new NotImplementedException(e.ToString());
    }

    private static ILoweredExpression LowerValueAssignment(
            Expressions.IExpression left,
            Expressions.IExpression right,
            bool valueUseful)
    {
        if (left is Expressions.ValueAccessorExpression valueAccessor)
        {
            var variable = valueAccessor.ReferencedVariable.NotNull();
            if (variable is TypeChecking.TypeChecker.LocalVariable localVariable)
            {
                if (localVariable.ReferencedInClosure)
                {
                    throw new NotImplementedException();
                }

                return new LocalAssignmentExpression(
                        localVariable.Name.StringValue,
                        LowerExpression(right),
                        ProgramAbseil.GetTypeReference(localVariable.Type),
                        valueUseful);
            }
            throw new NotImplementedException(variable.ToString());
        }
        throw new NotImplementedException(left.ToString());
    }
}
