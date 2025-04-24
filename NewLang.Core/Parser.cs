using System.Collections.Frozen;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace NewLang.Core;

public static class Parser
{
    public static LangProgram Parse(IEnumerable<Token> tokens)
    {
        using var tokensEnumerator = new PeekableEnumerator<Token>(tokens.GetEnumerator());

        var scope = GetScope(tokensEnumerator, closingToken: null, allowTailExpression: false);

        return new LangProgram(scope);
    }

    /// <summary>
    /// Get a scope. Expects first token to be the first token within the scope (or the closing token)
    /// </summary>
    /// <param name="tokens"></param>
    /// <param name="closingToken"></param>
    /// <param name="allowTailExpression"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    private static ProgramScope GetScope(
        PeekableEnumerator<Token> tokens,
        TokenType? closingToken,
        bool allowTailExpression)
    {
        var hasTailExpression = false;
        var foundClosingToken = false;

        var expressions = new List<Expression>();
        var functions = new List<LangFunction>();
                
        while (tokens.TryPeek(out var peeked))
        {
            if (peeked.Type == closingToken)
            {
                // pop the peeked closing token off
                tokens.MoveNext();
                foundClosingToken = true;
                break;
            }

            if (peeked.Type == TokenType.Fn)
            {
                functions.Add(GetFunction(tokens));
                continue;
            }

            var expression = PopExpression(tokens);
            if (!expression.HasValue)
            {
                continue;
            }
            
            if (hasTailExpression)
            {
                throw new InvalidOperationException("Tail expression must be at the end of a block");
            }

            if (!tokens.TryPeek(out peeked) || peeked.Type != TokenType.Semicolon)
            {
                if (!allowTailExpression)
                {
                    throw new InvalidOperationException("Tail expression is not allowed");
                }

                hasTailExpression = true;
            }
            else
            {
                // drop semicolon
                tokens.MoveNext();
                if (!IsValidStatement(expression.Value))
                {
                    throw new InvalidOperationException($"{expression.Value.ExpressionType} is not a valid statement");
                }
            } 
            
            expressions.Add(expression.Value);
        }

        if (!foundClosingToken && closingToken.HasValue)
        {
            throw new InvalidOperationException($"Expected {closingToken.Value}, got nothing");
        }

        return new ProgramScope(expressions, functions);
    }

    private static LangFunction GetFunction(PeekableEnumerator<Token> tokens)
    {
        if (!tokens.MoveNext() || tokens.Current.Type != TokenType.Fn)
        {
            throw new InvalidOperationException($"expected fn");
        }

        if (!tokens.MoveNext() || tokens.Current.Type != TokenType.Identifier)
        {
            throw new InvalidOperationException("Expected function name");
        }

        var nameToken = tokens.Current;

        if (!tokens.MoveNext() || tokens.Current.Type != TokenType.LeftParenthesis)
        {
            throw new InvalidOperationException("Expected (");
        }

        var parameterList = new List<FunctionParameter>();

        while (true)
        {
            if (!tokens.MoveNext())
            {
                throw new InvalidOperationException("Expected ) or parameter type");
            }

            if (tokens.Current.Type == TokenType.RightParenthesis)
            {
                break;
            }

            if (parameterList.Count != 0)
            {
                if (tokens.Current.Type != TokenType.Comma)
                {
                    throw new InvalidOperationException("Expected ,");
                }
                if (!tokens.MoveNext())
                {
                    throw new InvalidOperationException("Expected parameter type");
                }
            }

            var parameterType = GetTypeIdentifier(tokens.Current, tokens);

            if (!tokens.MoveNext() || tokens.Current.Type != TokenType.Identifier)
            {
                throw new InvalidOperationException("Expected parameter name");
            }

            parameterList.Add(new FunctionParameter(parameterType, tokens.Current));
        }

        if (!tokens.MoveNext() )
        {
            throw new InvalidOperationException("Expected { or :");
        }

        TypeIdentifier? returnType = null;

        if (tokens.Current.Type == TokenType.Colon)
        {
            if (!tokens.MoveNext())
            {
                throw new InvalidOperationException("Expected return type");
            }

            returnType = GetTypeIdentifier(tokens.Current, tokens);

            if (!tokens.MoveNext())
            {
                throw new InvalidOperationException("Expected {");
            }
        }

        if (tokens.Current.Type != TokenType.LeftBrace)
        {
            throw new InvalidOperationException("Expected {");
        }

        var functionScope = GetScope(tokens, closingToken: TokenType.RightBrace, allowTailExpression: true);

        return new LangFunction(nameToken, parameterList, returnType, functionScope);
    }

    private static bool IsTypeTokenType(in TokenType tokenType)
    {
        return tokenType is TokenType.IntKeyword or TokenType.StringKeyword or TokenType.Result or TokenType.Identifier;
    } 

    private static TypeIdentifier GetTypeIdentifier(Token firstToken, PeekableEnumerator<Token> tokens)
    {
        if (!IsTypeTokenType(firstToken.Type))
        {
            throw new InvalidOperationException("Expected type");
        }

        var typeArguments = new List<TypeIdentifier>();
        if (tokens.TryPeek(out var peeked) && peeked.Type == TokenType.LeftAngleBracket)
        {
            tokens.MoveNext();

            while (true)
            {
                if (!tokens.MoveNext())
                {
                    throw new InvalidOperationException("Expected type argument");
                }

                if (tokens.Current.Type == TokenType.RightAngleBracket)
                {
                    break;
                }

                if (typeArguments.Count != 0)
                {
                    if (tokens.Current.Type != TokenType.Comma)
                    {
                        throw new InvalidOperationException("Expected ,");
                    }
                    if (!tokens.MoveNext())
                    {
                        throw new InvalidOperationException("Expected type argument");
                    }
                }

                typeArguments.Add(GetTypeIdentifier(tokens.Current, tokens));
            }
        }

        return new TypeIdentifier(firstToken, typeArguments);
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
            TokenType.LeftParenthesis => GetMethodCall(tokens, previousExpression ?? throw new InvalidOperationException($"Unexpected token {token}")),
            TokenType.Dot => GetMemberAccess(tokens, previousExpression ?? throw new InvalidOperationException($"Unexpected token {token}")),
            _ => throw new InvalidOperationException($"Token type {token.Type} not supported")
        };
    }

    public static Expression? PopExpression(IEnumerable<Token> tokens)
    {
        using var enumerator = new PeekableEnumerator<Token>(tokens.GetEnumerator());

        return PopExpression(enumerator);
    }

    private static Expression? PopExpression(PeekableEnumerator<Token> tokens, uint? currentBindingStrength = null)
    {
        Expression? previousExpression = null;
        while (tokens.TryPeek(out var peeked) 
               && peeked.Type != TokenType.Semicolon)
        {
            tokens.MoveNext();
            previousExpression = MatchTokenToExpression(tokens.Current, previousExpression, tokens);
            
            if (!tokens.TryPeek(out peeked) 
                || !TryGetBindingStrength(peeked, out var bindingStrength)
                || bindingStrength <= currentBindingStrength)
            {
                break;
            }
        }

        return previousExpression;
    }

    private static Expression GetMemberAccess(PeekableEnumerator<Token> tokens, Expression memberOwner)
    {
        if (!tokens.MoveNext() || tokens.Current.Type != TokenType.Identifier)
        {
            throw new InvalidOperationException("Expected member identifier");
        }

        return new Expression(new MemberAccess(memberOwner, tokens.Current));
    }

    private static Expression GetMethodCall(PeekableEnumerator<Token> tokens, Expression method)
    {
        var parameterList = new List<Expression>();
        while (tokens.TryPeek(out var peeked))
        {
            if (peeked.Type == TokenType.RightParenthesis)
            {
                return new Expression(new MethodCall(method, parameterList));
            }

            if (parameterList.Count > 0)
            {
                if (peeked.Type != TokenType.Comma)
                {
                    throw new InvalidOperationException("Expected comma");
                }

                // pop comma off
                tokens.MoveNext();
            }

            var nextExpression = PopExpression(tokens);
            if (nextExpression.HasValue)
            {
                parameterList.Add(nextExpression.Value);
            }
        }

        throw new InvalidOperationException("Expected ), found nothing");
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

                elseIfs.Add(new ElseIf(elseIfCheckExpression, elseIfBody));
            }
            else
            {
                elseBody = PopExpression(tokens)
                                 ?? throw new InvalidOperationException("Expected else body, got nothing");
                break;
            }
        }

        return new Expression(new IfExpression(
            checkExpression.Value,
            body,
            elseIfs,
            elseBody));
    }

    private static Expression GetBlockExpression(PeekableEnumerator<Token> tokens)
    {
        var scope = GetScope(tokens, TokenType.RightBrace, true);
        
        return new Expression(new Block(scope));
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

        return new Expression(new VariableDeclaration(identifier, valueExpression.Value));
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

        return new Expression(new BinaryOperator(operatorType, leftOperand, right.Value, operatorToken));
    }

    private static bool TryGetBindingStrength(Token token, [NotNullWhen(true)] out uint? bindingStrength)
    {
        bindingStrength = token.Type switch
        {
            // unary operators
            TokenType.QuestionMark => UnaryOperatorBindingStrengths[UnaryOperatorType.FallOut],
            // binary operators
            TokenType.RightAngleBracket => BinaryOperatorBindingStrengths[BinaryOperatorType.GreaterThan],
            TokenType.LeftAngleBracket => BinaryOperatorBindingStrengths[BinaryOperatorType.LessThan],
            TokenType.Star => BinaryOperatorBindingStrengths[BinaryOperatorType.Multiply],
            TokenType.ForwardSlash => BinaryOperatorBindingStrengths[BinaryOperatorType.Divide],
            TokenType.Plus => BinaryOperatorBindingStrengths[BinaryOperatorType.Plus],
            TokenType.Dash => BinaryOperatorBindingStrengths[BinaryOperatorType.Minus],
            TokenType.LeftParenthesis => 4,
            TokenType.Dot => 5,
            _ => null
        };

        return bindingStrength.HasValue;
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
            { UnaryOperatorType.FallOut, 6 },
        }.ToFrozenDictionary();
}