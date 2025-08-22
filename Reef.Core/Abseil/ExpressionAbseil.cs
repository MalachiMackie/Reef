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
            Expressions.UnaryOperatorExpression e => LowerUnaryOperatorExpression(e),
            Expressions.BlockExpression e => LowerBlockExpression(e), 
            _ => throw new NotImplementedException($"{expression.GetType()}")
        };
    }

    private static ILoweredExpression LowerBlockExpression(Expressions.BlockExpression e)
    {
        foreach (var method in e.Block.Functions)
        {
            throw new NotImplementedException();
        }

        return new BlockExpression(
                [..e.Block.Expressions.Select(LowerExpression)],
                ProgramAbseil.GetTypeReference(e.ResolvedType.NotNull()),
                e.ValueUseful);
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

    private static ILoweredExpression LowerUnaryOperatorExpression(
            Expressions.UnaryOperatorExpression e)
    {
        var operand = LowerExpression(e.UnaryOperator.Operand.NotNull());
        switch (e.UnaryOperator.OperatorType)
        {
            case Expressions.UnaryOperatorType.FallOut:
                break;
            case Expressions.UnaryOperatorType.Not:
                return new BoolNotExpression(e.ValueUseful, operand);
        }

        throw new NotImplementedException(e.UnaryOperator.OperatorType.ToString());
    }

    private static ILoweredExpression LowerValueAccessorExpression(
            Expressions.ValueAccessorExpression e)
    {
        return e switch
        {
            { ValueAccessor: { AccessType: Expressions.ValueAccessType.Literal, Token: StringToken { StringValue: var stringLiteral } } } => new StringConstantExpression(e.ValueUseful, stringLiteral),
            { ValueAccessor: { AccessType: Expressions.ValueAccessType.Literal, Token: IntToken { Type: TokenType.IntLiteral, IntValue: var intValue} }} => new IntConstantExpression(e.ValueUseful, intValue),
            { ValueAccessor: { AccessType: Expressions.ValueAccessType.Literal, Token.Type: TokenType.True }} => new BoolConstantExpression(e.ValueUseful, true),
            { ValueAccessor: { AccessType: Expressions.ValueAccessType.Literal, Token.Type: TokenType.False }} => new BoolConstantExpression(e.ValueUseful, false),
            _ => throw new NotImplementedException($"e")
        };
    }

    private static ILoweredExpression LowerBinaryOperatorExpression(
            Expressions.BinaryOperatorExpression e)
    {
        if (e.BinaryOperator.OperatorType == Expressions.BinaryOperatorType.ValueAssignment)
        {
            return LowerValueAssignment(
                    e.BinaryOperator.Left.NotNull(),
                    e.BinaryOperator.Right.NotNull(),
                    e.ValueUseful);
        }

        var left = LowerExpression(e.BinaryOperator.Left.NotNull());
        var right = LowerExpression(e.BinaryOperator.Right.NotNull());

        return e.BinaryOperator.OperatorType switch
        {
            Expressions.BinaryOperatorType.LessThan
                => new IntLessThanExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.GreaterThan
                => new IntGreaterThanExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.Plus
                => new IntPlusExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.Minus
                => new IntMinusExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.Multiply
                => new IntMultiplyExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.Divide
                => new IntDivideExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.EqualityCheck
                // todo: handle more types of equality checks 
                => new IntEqualsExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.BooleanAnd
                => new BoolAndExpression(e.ValueUseful, left, right),
            Expressions.BinaryOperatorType.BooleanOr
                => new BoolOrExpression(e.ValueUseful, left, right),
            _ => throw new InvalidOperationException($"Invalid binary operator {e.BinaryOperator.OperatorType}"),
        };
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
