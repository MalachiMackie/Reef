using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace NewLang.Core;

public static class ExpressionTreeBuilder
{
    public static IEnumerable<Expression> Build(IEnumerable<Token> tokens)
    {
        var expressionStack = new Stack<Expression>();
        
        using var tokensEnumerator = new PeekableEnumerator<Token>(tokens.GetEnumerator());
        var expression = PopExpression(tokensEnumerator, expressionStack);
        while (expression.HasValue)
        {
            expressionStack.Push(expression.Value);

            expression = PopExpression(tokensEnumerator, expressionStack);
        }

        return expressionStack;
    }

    private static Expression? PopExpression(PeekableEnumerator<Token> tokens, Stack<Expression> expressionStack)
    {
        if (!tokens.MoveNext())
        {
            return null;
        }

        var token = tokens.Current;
        return token.Type switch
        {
            // value accessors
            TokenType.Identifier => new Expression(new ValueAccessor(ValueAccessType.Variable, token)),
            TokenType.StringLiteral or TokenType.IntLiteral or TokenType.True or TokenType.False => new Expression(
                new ValueAccessor(ValueAccessType.Literal, token)),
            // unary operators
            TokenType.QuestionMark => GetUnaryOperatorExpression(tokens, expressionStack, token,
                UnaryOperatorType.FallOut),
            // binary operator tokens
            TokenType.LeftAngleBracket => GetBinaryOperatorExpression(tokens, expressionStack, token,
                BinaryOperatorType.LessThan),
            TokenType.RightAngleBracket => GetBinaryOperatorExpression(tokens, expressionStack, token,
                BinaryOperatorType.GreaterThan),
            TokenType.Plus => GetBinaryOperatorExpression(tokens, expressionStack, token, BinaryOperatorType.Plus),
            TokenType.Dash => GetBinaryOperatorExpression(tokens, expressionStack, token, BinaryOperatorType.Minus),
            TokenType.Star => GetBinaryOperatorExpression(tokens, expressionStack, token, BinaryOperatorType.Multiply),
            TokenType.ForwardSlash => GetBinaryOperatorExpression(tokens, expressionStack, token,
                BinaryOperatorType.Divide),
            _ => throw new InvalidOperationException($"Token type {token.Type} not supported")
        };
    }
    
    private static Expression GetUnaryOperatorExpression(
        PeekableEnumerator<Token> tokens,
        Stack<Expression> expressionStack,
        Token operatorToken,
        UnaryOperatorType operatorType)
    {
        var expression = expressionStack.Pop();
        
        if (tokens.TryPeek(out var nextToken)
            && TryGetBindingStrength(nextToken, out var nextBindingStrength)
            && nextBindingStrength > UnaryOperatorBindingStrengths[operatorType])
        {
            // the next token has a higher binding strength than the operator we're trying to build,
            // so push our expression back onto the stack so that the next expression can use it
            expressionStack.Push(expression);
            var nextExpression = PopExpression(tokens, expressionStack);
            if (nextExpression is null)
            {
                throw new Exception("Expected expression but got null");
            }

            expression = nextExpression.Value;
        }

        return new Expression(new UnaryOperator(operatorType, expression, operatorToken));
    }
    
    private static Expression GetBinaryOperatorExpression(
        PeekableEnumerator<Token> tokens,
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

        if (tokens.TryPeek(out var nextToken)
            && TryGetBindingStrength(nextToken, out var nextBindingStrength)
            && nextBindingStrength > BinaryOperatorBindingStrengths[operatorType])
        {
            // the next token has a higher binding strength than the operator we're trying to build,
            // so push our right expression back onto the stack so that the next expression can use it
            expressionStack.Push(right.Value);
            right = PopExpression(tokens, expressionStack);
            if (right is null)
            {
                throw new Exception("Expected expression but got null");
            }
        }

        return new Expression(new BinaryOperator(operatorType, left, right.Value, operatorToken));
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