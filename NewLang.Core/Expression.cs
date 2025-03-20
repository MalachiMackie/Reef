using System.Runtime.CompilerServices;

namespace NewLang.Core;

public record struct Expression(
    ExpressionType Type,
    ValueAccessor? ValueAccessor,
    StrongBox<UnaryOperator>? UnaryOperator,
    StrongBox<BinaryOperator>? BinaryOperator)
{
    public Expression(ValueAccessor valueAccessor)
        : this(ExpressionType.ValueAccess, valueAccessor, null, null)
    {
        
    }
    
    public Expression(UnaryOperator unaryOperator)
        : this(ExpressionType.UnaryOperator, null, new StrongBox<UnaryOperator>(unaryOperator), null)
    {
    }

    public Expression(BinaryOperator binaryOperator)
        : this(ExpressionType.BinaryOperator, null, null, new StrongBox<BinaryOperator>(binaryOperator))
    {
    }
}

public record struct ValueAccessor(ValueAccessType AccessType, Token Token);

public record struct UnaryOperator()
{
}

public record struct BinaryOperator(BinaryOperatorType Type, Expression Left, Expression Right, Token OperatorToken);

public enum BinaryOperatorType
{
    LessThan,
    GreaterThan
}

public enum ValueAccessType
{
    Variable,
    Literal
}

public enum UnaryOperatorType
{
    //Not
}

public enum ExpressionType
{
    ValueAccess,
    UnaryOperator,
    BinaryOperator
}

public class ExpressionBuilder
{
    public IEnumerable<Expression> GetExpressions(IEnumerable<Token> tokens)
    {
        var expressionStack = new Stack<Expression>();
        
        using var tokensEnumerator = tokens.GetEnumerator();
        var expression = PopExpression(tokensEnumerator, expressionStack);
        while (expression.HasValue)
        {
            expressionStack.Push(expression.Value);

            expression = PopExpression(tokensEnumerator, expressionStack);
        }

        return expressionStack;
    }

    private static Expression? PopExpression(IEnumerator<Token> tokens, Stack<Expression> expressionStack)
    {
        while (tokens.MoveNext())
        {
            var token = tokens.Current;
            switch (token.Type)
            {
                // value accessors
                case TokenType.Identifier:
                    return new Expression(new ValueAccessor(ValueAccessType.Variable, token));
                case TokenType.StringLiteral:
                case TokenType.IntLiteral:
                case TokenType.True:
                case TokenType.False:
                    return new Expression(new ValueAccessor(ValueAccessType.Literal, token));
                // binary operator tokens
                case TokenType.LeftAngleBracket:
                    return GetBinaryOperatorExpression(tokens, expressionStack, token, BinaryOperatorType.LessThan);
                case TokenType.RightAngleBracket:
                    return GetBinaryOperatorExpression(tokens, expressionStack, token, BinaryOperatorType.GreaterThan);
                default:
                    throw new NotImplementedException();
            }
        }

        return null;
    }

    private static Expression GetBinaryOperatorExpression(
        IEnumerator<Token> tokens,
        Stack<Expression> expressionStack,
        Token operatorToken,
        BinaryOperatorType operatorType)
    {
        var left = expressionStack.Pop();
        var right = PopExpression(tokens, expressionStack);
        if (right is null)
        {
            throw new Exception("Expected expression but got null");
        }

        return new Expression(new BinaryOperator(operatorType, left, right.Value, operatorToken));
    }
}