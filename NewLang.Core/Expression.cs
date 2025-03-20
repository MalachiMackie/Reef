namespace NewLang.Core;

public record struct Expression(ExpressionType Type, IEnumerable<Token> Tokens)
{
    public static Expression VariableAccess(IEnumerable<Token> tokens)
    {
        return new Expression(ExpressionType.VariableAccess, tokens);
    }
}

public enum ExpressionType
{
    VariableAccess
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
                case TokenType.Identifier:
                    return Expression.VariableAccess([token]);
                default:
                    throw new NotImplementedException();
            }
        }

        return null;
    }
}