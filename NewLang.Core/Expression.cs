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

public record struct BinaryOperator(BinaryOperatorType Type, Expression Left, Expression Right);

public enum BinaryOperatorType
{
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
        using var tokensEnumerator = tokens.GetEnumerator();
        var expression = PopExpression(tokensEnumerator);
        while (expression.HasValue)
        {
            yield return expression.Value;
            expression = PopExpression(tokensEnumerator);
        }
    }

    private Expression? PopExpression(IEnumerator<Token> tokens)
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
                default:
                    throw new NotImplementedException();
            }
        }

        return null;
    }
}