using System.Collections.Frozen;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace NewLang.Core;

public static class ExpressionTreeBuilder
{
    public static IEnumerable<Expression> Build(IEnumerable<Token> tokens)
    {
        using var tokensEnumerator = new PeekableEnumerator<Token>(tokens.GetEnumerator());
        while (tokensEnumerator.TryPeek(out _))
        {
            var result = PopExpression(tokensEnumerator);
            if (result.HasValue)
            {
                var (expression, isTailExpression) = result.Value;

                if (isTailExpression)
                {
                    throw new InvalidOperationException("Top level statements cannot have tail expressions");
                }

                if (!IsValidStatement(expression))
                {
                    throw new InvalidOperationException($"{expression.ExpressionType} is not a valid statement");
                }
                
                yield return expression;
            }
        }
    }

    private static bool IsValidStatement(Expression expression)
    {
        return expression.ExpressionType is 
            ExpressionType.Block 
            or ExpressionType.IfExpression
            or ExpressionType.VariableDeclaration;
    }
    
    private static Expression MatchTokenToExpression(Token token, Expression? previousExpression, PeekableEnumerator<Token> tokens)
    {
        return token.Type switch
        {
            // value accessors
            TokenType.Identifier => new Expression(new ValueAccessor(ValueAccessType.Variable, token)),
            TokenType.StringLiteral or TokenType.IntLiteral or TokenType.True or TokenType.False => new Expression(
                new ValueAccessor(ValueAccessType.Literal, token)),
            // unary operators
            TokenType.QuestionMark => GetUnaryOperatorExpression(previousExpression ?? throw new InvalidOperationException($"Unexpected token {token}"), token, UnaryOperatorType.FallOut),
            // binary operator tokens
            TokenType.LeftAngleBracket => GetBinaryOperatorExpression(tokens, previousExpression ?? throw new InvalidOperationException($"Unexpected token {token}"), token,
                BinaryOperatorType.LessThan),
            TokenType.RightAngleBracket => GetBinaryOperatorExpression(tokens, previousExpression ?? throw new InvalidOperationException($"Unexpected token {token}"), token,
                BinaryOperatorType.GreaterThan),
            TokenType.Plus => GetBinaryOperatorExpression(tokens, previousExpression ?? throw new InvalidOperationException($"Unexpected token {token}"), token, BinaryOperatorType.Plus),
            TokenType.Dash => GetBinaryOperatorExpression(tokens, previousExpression ?? throw new InvalidOperationException($"Unexpected token {token}"), token, BinaryOperatorType.Minus),
            TokenType.Star => GetBinaryOperatorExpression(tokens, previousExpression ?? throw new InvalidOperationException($"Unexpected token {token}"), token, BinaryOperatorType.Multiply),
            TokenType.ForwardSlash => GetBinaryOperatorExpression(tokens, previousExpression ?? throw new InvalidOperationException($"Unexpected token {token}"), token,
                BinaryOperatorType.Divide),
            TokenType.Var => GetVariableDeclaration(tokens),
            TokenType.LeftBrace => GetBlockExpression(tokens),
            TokenType.If => GetIfExpression(tokens),
            TokenType.Semicolon => throw new UnreachableException("PopExpression should have handled semicolon"),
            _ => throw new InvalidOperationException($"Token type {token.Type} not supported")
        };
    }

    public static Expression? PopExpression(IEnumerable<Token> tokens)
    {
        using var enumerator = new PeekableEnumerator<Token>(tokens.GetEnumerator());

        return PopExpression(enumerator)?.Expression;
    }

    private static (Expression Expression, bool IsTailExpression)? PopExpression(PeekableEnumerator<Token> tokens, uint? currentBindingStrength = null)
    {
        Expression? previousExpression = null;
        while (tokens.MoveNext() && tokens.Current.Type != TokenType.Semicolon)
        {
            previousExpression = MatchTokenToExpression(tokens.Current, previousExpression, tokens);

            // if we cannot bind to the next expression, return our current expression
            if (!tokens.TryPeek(out var peeked)
                || !TryGetBindingStrength(peeked, out var bindingStrength)
                || bindingStrength <= currentBindingStrength)
            {
                var isTailExpression = peeked.Type != TokenType.Semicolon
                                       && previousExpression.Value switch
                                       {
                                           {
                                               ExpressionType: ExpressionType.Block,
                                           } => previousExpression.Value.Block!.Value.TailExpression is not null,
                                           {
                                               ExpressionType: ExpressionType.IfExpression,
                                           } => previousExpression.Value.IfExpression!.Value.HasTailExpressions,
                                           _ => true
                                       };
                
                return (
                    previousExpression.Value,
                    isTailExpression);
            }
        }

        return null;
    }

    private static Expression GetIfExpression(PeekableEnumerator<Token> tokens)
    {
        if (!tokens.MoveNext())
        {
            throw new InvalidOperationException("Expected left parenthesis, found nothing");
        }

        if (tokens.Current.Type != TokenType.LeftParenthesis)
        {
            throw new InvalidOperationException($"Expected left parenthesis, found {tokens.Current.Type}");
        }

        var checkExpression = PopExpression(tokens);
        if (checkExpression is null)
        {
            throw new InvalidOperationException("Expected check expression, found no expression");
        }

        if (!tokens.MoveNext())
        {
            throw new InvalidOperationException("Expected right parenthesis, found nothing");
        }

        if (tokens.Current.Type != TokenType.RightParenthesis)
        {
            throw new InvalidOperationException($"Expected right parenthesis, found {tokens.Current.Type}");
        }

        var body = PopExpression(tokens) ??
            throw new InvalidOperationException("Expected if body, found nothing");

        var elseIfs = new List<ElseIf>();
        Expression? elseBody = null;
        var hasTailExpression = body.IsTailExpression;

        while (tokens.TryPeek(out var peeked) && peeked.Type == TokenType.Else)
        {
            // pop the else token off
            tokens.MoveNext();

            if (tokens.TryPeek(out var nextPeeked) && nextPeeked.Type == TokenType.If)
            {
                // pop the if token off
                tokens.MoveNext();
                if (!tokens.MoveNext() || tokens.Current.Type != TokenType.LeftParenthesis)
                {
                    throw new InvalidOperationException("Expected left Parenthesis");
                }

                var elseIfCheckExpression = PopExpression(tokens)
                                            ?? throw new InvalidOperationException("Expected check expression");

                if (!tokens.MoveNext() || tokens.Current.Type != TokenType.RightParenthesis)
                {
                    throw new InvalidOperationException("Expected right Parenthesis");
                }

                var elseIfBody = PopExpression(tokens)
                    ?? throw new InvalidOperationException("Expected else if body");

                if (hasTailExpression != elseIfBody.IsTailExpression)
                {
                    throw new InvalidOperationException("Either all branches or no branch can have tail expressions");
                }

                elseIfs.Add(new ElseIf(elseIfCheckExpression.Expression, elseIfBody.Expression));
            }
            else
            {
                var elseResult = PopExpression(tokens)
                                 ?? throw new InvalidOperationException("Expected else body, got nothing");
                elseBody = elseResult.Expression;
                if (hasTailExpression != elseResult.IsTailExpression)
                {
                    throw new InvalidOperationException("Either all branch or no branches can have tail expressions");
                }
                break;
            }
        }

        if (hasTailExpression && elseBody is null)
        {
            throw new InvalidOperationException("Must have an else body for tail expressions");
        }
        
        return new Expression(new IfExpression(
            checkExpression.Value.Expression,
            body.Expression,
            elseIfs,
            elseBody,
            hasTailExpression));
    }

    private static Expression GetBlockExpression(PeekableEnumerator<Token> tokens)
    {
        var blockExpressions = new List<Expression>();

        Expression? tailExpression = null;
        
        while (tokens.TryPeek(out var peeked))
        {
            if (peeked.Type == TokenType.RightBrace)
            {
                // pop the peeked right brace off
                tokens.MoveNext();
                return new Expression(new Block(blockExpressions, tailExpression));
            }

            var expression = PopExpression(tokens);
            if (expression.HasValue)
            {
                if (tailExpression.HasValue)
                {
                    throw new InvalidOperationException("Tail expression must be at the end of a block");
                }

                if (expression.Value.IsTailExpression)
                {
                    tailExpression = expression.Value.Expression;
                }
                else
                {
                    if (!IsValidStatement(expression.Value.Expression))
                    {
                        throw new InvalidOperationException($"{expression.Value.Expression.ExpressionType} is not a valid statement");
                    }
                    blockExpressions.Add(expression.Value.Expression);
                }
            }
        }

        throw new InvalidOperationException("Expected Right brace, got nothing");
    }

    private static Expression GetVariableDeclaration(PeekableEnumerator<Token> tokens)
    {
        if (!tokens.MoveNext())
        {
            throw new InvalidOperationException("Expected variable identifier, got nothing");
        }

        var identifier = tokens.Current;
        if (identifier.Type != TokenType.Identifier)
        {
            throw new InvalidOperationException($"Expected variable identifier, got {identifier}");
        }

        if (!tokens.MoveNext())
        {
            throw new InvalidOperationException("Expected equals tokens, got nothing");
        }

        if (tokens.Current.Type != TokenType.Equals)
        {
            throw new InvalidOperationException($"Expected equals token, got {tokens.Current}");
        }

        var valueExpression = PopExpression(tokens);
        if (valueExpression is null)
        {
            throw new InvalidOperationException("Expected value expression, got nothing");
        }

        return new Expression(new VariableDeclaration(identifier, valueExpression.Value.Expression));
    }
    
    private static Expression GetUnaryOperatorExpression(
        Expression operand,
        Token operatorToken,
        UnaryOperatorType operatorType)
    {
        return new Expression(new UnaryOperator(operatorType, operand, operatorToken));
    }
    
    private static Expression GetBinaryOperatorExpression(
        PeekableEnumerator<Token> tokens,
        Expression leftOperand,
        Token operatorToken,
        BinaryOperatorType operatorType)
    {
        if (!TryGetBindingStrength(operatorToken, out var bindingStrength))
        {
            throw new UnreachableException("All operators have a binding strength");
        }
        var right = PopExpression(tokens, bindingStrength.Value);
        if (right is null)
        {
            throw new Exception("Expected expression but got null");
        }

        return new Expression(new BinaryOperator(operatorType, leftOperand, right.Value.Expression, operatorToken));
    }

    private static bool TryGetBindingStrength(Token token, [NotNullWhen(true)] out uint? bindingStrength)
    {
        (UnaryOperatorType? unaryOperatorType, BinaryOperatorType? binaryOperatorType) operators = token.Type switch
        {
            // unary operators
            TokenType.QuestionMark => (UnaryOperatorType.FallOut, null),
            // binary operators
            TokenType.RightAngleBracket => (null, BinaryOperatorType.GreaterThan),
            TokenType.LeftAngleBracket => (null, BinaryOperatorType.LessThan),
            TokenType.Star => (null, BinaryOperatorType.Multiply),
            TokenType.ForwardSlash => (null, BinaryOperatorType.Divide),
            TokenType.Plus => (null, BinaryOperatorType.Plus),
            TokenType.Dash => (null, BinaryOperatorType.Minus),
            _ => (null, null)
        };

        if (operators.unaryOperatorType.HasValue)
        {
            bindingStrength = UnaryOperatorBindingStrengths[operators.unaryOperatorType.Value];
            return true;
        }

        if (operators.binaryOperatorType.HasValue)
        {
            bindingStrength = BinaryOperatorBindingStrengths[operators.binaryOperatorType.Value];
            return true;
        }
        
        bindingStrength = null;
        return false;
    }
    
    private static readonly FrozenDictionary<BinaryOperatorType, uint> BinaryOperatorBindingStrengths =
        new Dictionary<BinaryOperatorType, uint>
        {
            { BinaryOperatorType.Multiply, 3 },
            { BinaryOperatorType.Divide, 3 },
            { BinaryOperatorType.Plus, 2 },
            { BinaryOperatorType.Minus, 2 },
            { BinaryOperatorType.GreaterThan, 1 },
            { BinaryOperatorType.LessThan, 1 },
        }.ToFrozenDictionary();
    
    private static readonly FrozenDictionary<UnaryOperatorType, uint> UnaryOperatorBindingStrengths =
        new Dictionary<UnaryOperatorType, uint>
        {
            { UnaryOperatorType.FallOut, 4 },
        }.ToFrozenDictionary();
}