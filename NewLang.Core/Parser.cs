using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace NewLang.Core;

public static class Parser
{
    public static LangProgram Parse(IEnumerable<Token> tokens)
    {
        using var tokensEnumerator = new PeekableEnumerator<Token>(tokens.GetEnumerator());

        var (expressions, functions, classes, fields) = GetScope(tokensEnumerator, closingToken: null);

        if (fields.Count > 0)
        {
            throw new InvalidOperationException("A field is not a valid statement");
        }

        return new LangProgram(expressions, functions, classes);
    }

    /// <summary>
    /// Get a scope. Expects first token to be the first token within the scope (or the closing token)
    /// </summary>
    /// <param name="tokens"></param>
    /// <param name="closingToken"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    private static (
        IReadOnlyList<Expression> Expressions,
        IReadOnlyList<LangFunction> Functions,
        IReadOnlyList<ProgramClass> Classes,
        IReadOnlyList<ClassField> Fields) GetScope(
        PeekableEnumerator<Token> tokens,
        TokenType? closingToken)
    {
        var hasTailExpression = false;
        var foundClosingToken = false;

        var expressions = new List<Expression>();
        var functions = new List<LangFunction>();
        var fields = new List<ClassField>();
        var classes = new List<ProgramClass>();
                
        while (tokens.TryPeek(out var peeked))
        {
            if (peeked.Type == closingToken)
            {
                // pop the peeked closing token off
                tokens.MoveNext();
                foundClosingToken = true;
                break;
            }

            if (IsMember(peeked.Type))
            {
                tokens.MoveNext();
                var (function, @class, field) = GetMember(tokens);
                if (function.HasValue)
                {
                    functions.Add(function.Value);
                }
                else if (@class.HasValue)
                {
                    classes.Add(@class.Value);
                }
                else if (field.HasValue)
                {
                    fields.Add(field.Value);

                    if (!tokens.MoveNext() || tokens.Current.Type != TokenType.Semicolon)
                    {
                        throw new InvalidOperationException("Expected ;");
                    }
                }

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
                if (expression.Value.ExpressionType == ExpressionType.MethodReturn)
                {
                    throw new InvalidOperationException("Return statement cannot be a tail expression");
                }

                hasTailExpression = expression.Value.ExpressionType is not (ExpressionType.IfExpression or ExpressionType.Block);
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

        return (expressions, functions, classes, fields);
    }

    private static bool IsMember(TokenType tokenType)
    {
        return tokenType is TokenType.Pub or TokenType.Mut or TokenType.Static or TokenType.Fn or TokenType.Class or TokenType.Field;
    }

    private static (LangFunction? Function, ProgramClass? Class, ClassField? Field) GetMember(PeekableEnumerator<Token> tokens)
    {
        AccessModifier? accessModifier = null;
        MutabilityModifier? mutabilityModifier = null;
        StaticModifier? staticModifier = null;

        if (tokens.Current.Type == TokenType.Pub)
        {
            accessModifier = new AccessModifier(tokens.Current);
            if (!tokens.MoveNext())
            {
                throw new InvalidOperationException("Expected field, mut, static, fn or class");
            }
        }
        
        if (tokens.Current.Type == TokenType.Static)
        {
            staticModifier = new StaticModifier(tokens.Current);
            if (!tokens.MoveNext())
            {
                throw new InvalidOperationException("Expected field, fn or class");
            }
        }
        
        if (tokens.Current.Type == TokenType.Mut)
        {
            mutabilityModifier = new MutabilityModifier(tokens.Current);
            if (!tokens.MoveNext())
            {
                throw new InvalidOperationException("Expected field, fn, static or class");
            }
        }

        if (tokens.Current.Type == TokenType.Fn)
        {
            if (mutabilityModifier.HasValue)
            {
                throw new InvalidOperationException("Function cannot have mutability modifier");
            }

            var function = GetFunctionDeclaration(accessModifier, staticModifier, tokens);
            return (function, null, null);
        }

        if (tokens.Current.Type == TokenType.Class)
        {
            if (mutabilityModifier.HasValue)
            {
                throw new InvalidOperationException("Class cannot have a mutability modifier");
            }

            if (staticModifier.HasValue)
            {
                throw new InvalidOperationException("Class cannot be static");
            }

            var @class = GetClass(accessModifier, tokens);

            return (null, @class, null);
        }

        if (tokens.Current.Type == TokenType.Field)
        {
            var field = GetField(accessModifier, staticModifier, mutabilityModifier, tokens);

            return (null, null, field);
        }

        throw new InvalidOperationException("Expected class, fn or field");
    }

    private static ClassField GetField(AccessModifier? accessModifier, StaticModifier? staticModifier, MutabilityModifier? mutabilityModifier, PeekableEnumerator<Token> tokens)
    {
        // pub mut field MyField: string;
        if (!tokens.MoveNext() || tokens.Current.Type != TokenType.Identifier)
        {
            throw new InvalidOperationException("Expected field name");
        }

        var name = tokens.Current;

        if (!tokens.MoveNext() || tokens.Current.Type != TokenType.Colon)
        {
            throw new InvalidOperationException("Expected :");
        }

        if (!tokens.MoveNext())
        {
            throw new InvalidOperationException("Expected field type");
        }

        var type = GetTypeIdentifier(tokens);

        return new ClassField(accessModifier, staticModifier, mutabilityModifier, name, type);
    }

    private static ProgramClass GetClass(AccessModifier? accessModifier, PeekableEnumerator<Token> tokens)
    {
        if (!tokens.MoveNext() || tokens.Current.Type != TokenType.Identifier)
        {
            throw new InvalidOperationException("Expected class name");
        }

        var name = tokens.Current;

        if (!tokens.MoveNext())
        {
            throw new InvalidOperationException("Expected or <");
        }

        var typeArguments = new List<Token>();

        if (tokens.Current.Type == TokenType.LeftAngleBracket)
        {
            while (true)
            {
                if (!tokens.MoveNext())
                {
                    throw new InvalidOperationException("Expected Type Argument");
                }

                if (tokens.Current.Type == TokenType.RightAngleBracket)
                {
                    if (typeArguments.Count == 0)
                    {
                        throw new InvalidOperationException("Expected type argument");
                    }

                    if (!tokens.MoveNext())
                    {
                        throw new InvalidOperationException("Expected {");
                    }
                    break;
                }

                if (typeArguments.Count > 0)
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

                if (tokens.Current.Type != TokenType.Identifier)
                {
                    throw new InvalidOperationException("Expected type argument");
                }

                typeArguments.Add(tokens.Current);
            }
        }

        if (tokens.Current.Type != TokenType.LeftBrace)
        {
            throw new InvalidOperationException("Expected {");
        }

        var (expressions, functions, classes, fields) = GetScope(tokens, closingToken: TokenType.RightBrace);
        if (expressions.Count > 0)
        {
            throw new InvalidOperationException("Class cannot contain expressions");
        }
        if (classes.Count > 0)
        {
            throw new InvalidOperationException("Classes connot contain classes");
        }

        return new ProgramClass(accessModifier, name, typeArguments, functions, fields);
    }

    private static LangFunction GetFunctionDeclaration(AccessModifier? accessModifier, StaticModifier? staticModifier, PeekableEnumerator<Token> tokens)
    {
        if (!tokens.MoveNext() || tokens.Current.Type != TokenType.Identifier)
        {
            throw new InvalidOperationException("Expected function name");
        }

        var nameToken = tokens.Current;

        if (!tokens.MoveNext())
        {
            throw new InvalidOperationException("Expected ( or <");
        }

        var typeArguments = new List<Token>();

        if (tokens.Current.Type == TokenType.LeftAngleBracket)
        {
            while (true)
            {
                if (!tokens.MoveNext())
                {
                    throw new InvalidOperationException("Expected Type Argument");
                }

                if (tokens.Current.Type == TokenType.RightAngleBracket)
                {
                    if (typeArguments.Count == 0)
                    {
                        throw new InvalidOperationException("Expected type argument");
                    }

                    if (!tokens.MoveNext())
                    {
                        throw new InvalidOperationException("Expected (");
                    }
                    break;
                }

                if (typeArguments.Count > 0)
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

                if (tokens.Current.Type != TokenType.Identifier)
                {
                    throw new InvalidOperationException("Expected type argument");
                }

                typeArguments.Add(tokens.Current);
            }
        }

        if (tokens.Current.Type != TokenType.LeftParenthesis)
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

            if (tokens.Current.Type != TokenType.Identifier)
            {
                throw new InvalidOperationException("Expected parameter name");
            }
            
            var parameterName = tokens.Current;

            if (!tokens.MoveNext() || tokens.Current.Type != TokenType.Colon)
            {
                throw new InvalidOperationException("Expected :");
            }

            if (!tokens.MoveNext())
            {
                throw new InvalidOperationException("Expected parameter type");
            }
            
            var parameterType = GetTypeIdentifier(tokens);

            parameterList.Add(new FunctionParameter(parameterType, parameterName));
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

            returnType = GetTypeIdentifier(tokens);

            if (!tokens.MoveNext())
            {
                throw new InvalidOperationException("Expected {");
            }
        }

        var (expressions, functions, classes, fields) = GetScope(tokens, closingToken: TokenType.RightBrace);

        if (classes.Count > 0)
        {
            throw new InvalidOperationException("Functions cannot contain classes");
        }

        if (fields.Count > 0)
        {
            throw new InvalidOperationException("Functions cannot contain fields");
        }

        return new LangFunction(accessModifier, staticModifier, nameToken, typeArguments, parameterList, returnType, new Block(expressions, functions));
    }

    private static bool IsTypeTokenType(in TokenType tokenType)
    {
        return tokenType is TokenType.IntKeyword or TokenType.StringKeyword or TokenType.Result or TokenType.Identifier;
    } 

    private static TypeIdentifier GetTypeIdentifier(PeekableEnumerator<Token> tokens)
    {
        if (!IsTypeTokenType(tokens.Current.Type))
        {
            throw new InvalidOperationException("Expected type");
        }

        var typeIdentifier = tokens.Current;

        var typeArguments = new List<TypeIdentifier>();
        if (tokens.TryPeek(out var peeked) && peeked.Type == TokenType.Turbofish)
        {
            if (!tokens.MoveNext() && tokens.Current.Type != TokenType.LeftAngleBracket)
            {
                throw new InvalidOperationException("Expected <");
            }

            while (true)
            {
                if (!tokens.MoveNext())
                {
                    throw new InvalidOperationException("Expected type argument");
                }

                if (tokens.Current.Type == TokenType.RightAngleBracket)
                {
                    if (typeArguments.Count == 0)
                    {
                        throw new InvalidOperationException("Expected type argument");
                    }
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

                typeArguments.Add(GetTypeIdentifier(tokens));
            }
        }

        return new TypeIdentifier(typeIdentifier, typeArguments);
    }

    private static bool IsValidStatement(Expression expression)
    {
        return expression is
            {
                ExpressionType: ExpressionType.Block
                or ExpressionType.IfExpression
                or ExpressionType.VariableDeclaration
                or ExpressionType.MethodCall
                or ExpressionType.MethodReturn
            } or
            {
                ExpressionType: ExpressionType.BinaryOperator,
                BinaryOperator.Value.OperatorType: BinaryOperatorType.ValueAssignment
            };
    }
    
    private static Expression MatchTokenToExpression(Expression? previousExpression, PeekableEnumerator<Token> tokens)
    {
        return tokens.Current.Type switch
        {
            // value accessors
            TokenType.StringLiteral or TokenType.IntLiteral or TokenType.True or TokenType.False => new Expression(
                new ValueAccessor(ValueAccessType.Literal, tokens.Current)),
            // unary operators
            TokenType.QuestionMark => GetUnaryOperatorExpression(previousExpression ?? throw new InvalidOperationException($"Unexpected token {tokens.Current}"), tokens.Current, UnaryOperatorType.FallOut),
            // binary operator tokens
            TokenType.LeftAngleBracket => GetBinaryOperatorExpression(tokens, previousExpression ?? throw new InvalidOperationException($"Unexpected token {tokens.Current}"),
                BinaryOperatorType.LessThan),
            TokenType.RightAngleBracket => GetBinaryOperatorExpression(tokens, previousExpression ?? throw new InvalidOperationException($"Unexpected token {tokens.Current}"),
                BinaryOperatorType.GreaterThan),
            TokenType.Plus => GetBinaryOperatorExpression(tokens, previousExpression ?? throw new InvalidOperationException($"Unexpected token {tokens.Current}"), BinaryOperatorType.Plus),
            TokenType.Dash => GetBinaryOperatorExpression(tokens, previousExpression ?? throw new InvalidOperationException($"Unexpected token {tokens.Current}"), BinaryOperatorType.Minus),
            TokenType.Star => GetBinaryOperatorExpression(tokens, previousExpression ?? throw new InvalidOperationException($"Unexpected token {tokens.Current}"), BinaryOperatorType.Multiply),
            TokenType.ForwardSlash => GetBinaryOperatorExpression(tokens, previousExpression ?? throw new InvalidOperationException($"Unexpected token {tokens.Current}"),
                BinaryOperatorType.Divide),
            TokenType.Var => GetVariableDeclaration(tokens),
            TokenType.LeftBrace => GetBlockExpression(tokens),
            TokenType.If => GetIfExpression(tokens),
            TokenType.Semicolon => throw new UnreachableException("PopExpression should have handled semicolon"),
            TokenType.DoubleColon => GetBinaryOperatorExpression(tokens, previousExpression ?? throw new InvalidOperationException($"Unexpected token {tokens.Current}"), BinaryOperatorType.StaticMemberAccess),
            TokenType.LeftParenthesis => GetMethodCall(tokens, previousExpression ?? throw new InvalidOperationException($"Unexpected token {tokens.Current}")),
            TokenType.Turbofish => GetGenericInstantiation(tokens, previousExpression ?? throw new InvalidOperationException($"Unexpected token {tokens.Current}")),
            TokenType.Dot => GetBinaryOperatorExpression(tokens, previousExpression ?? throw new InvalidOperationException($"Unexpected token {tokens.Current}"), BinaryOperatorType.MemberAccess),
            TokenType.Return => GetMethodReturn(tokens),
            TokenType.New => GetObjectInitializer(tokens),
            TokenType.Equals => GetBinaryOperatorExpression(tokens, previousExpression ?? throw new InvalidOperationException($"Unexpected token {tokens.Current}"), BinaryOperatorType.ValueAssignment),
            TokenType.DoubleEquals => GetBinaryOperatorExpression(tokens, previousExpression ?? throw new InvalidOperationException($"Unexpected token {tokens.Current}"), BinaryOperatorType.EqualityCheck),
            TokenType.Ok or TokenType.Error => new Expression(new ValueAccessor(ValueAccessType.Variable, tokens.Current)),
            _ when IsTypeTokenType(tokens.Current.Type) => new Expression(new ValueAccessor(ValueAccessType.Variable, tokens.Current)),
            _ => throw new InvalidOperationException($"Token type {tokens.Current.Type} not supported")
        };
    }

    private static Expression GetGenericInstantiation(PeekableEnumerator<Token> tokens, Expression previousExpression)
    {
        var typeArguments = new List<TypeIdentifier>();
        
        while (true)
        {
            if (!tokens.MoveNext())
            {
                throw new InvalidOperationException("Expected type argument");
            }

            if (tokens.Current.Type == TokenType.RightAngleBracket)
            {
                if (typeArguments.Count == 0)
                {
                    throw new InvalidOperationException("Expected type argument");
                }

                break;
            }

            if (typeArguments.Count > 0)
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

            typeArguments.Add(GetTypeIdentifier(tokens));
        }

        return new Expression(new GenericInstantiation(previousExpression, typeArguments));
    }

    private static Expression GetObjectInitializer(PeekableEnumerator<Token> tokens)
    {
        if (!tokens.MoveNext())
        {
            throw new InvalidOperationException("Expected type");
        }

        var type = GetTypeIdentifier(tokens);

        if (!tokens.MoveNext() || tokens.Current.Type != TokenType.LeftBrace)
        {
            throw new InvalidOperationException("Expected {");
        }

        var fieldInitializers = new List<FieldInitializer>();

        while (true)
        {
            if (!tokens.MoveNext())
            {
                throw new InvalidOperationException("Expected field name");
            }

            if (tokens.Current.Type == TokenType.RightBrace)
            {
                // allow 0 field initializers
                break;
            }

            if (fieldInitializers.Count > 0)
            {
                if (tokens.Current.Type != TokenType.Comma)
                {
                    throw new InvalidOperationException("Expected ,");
                }

                if (!tokens.MoveNext())
                {
                    throw new InvalidOperationException("Expected field name");
                }
            }

            if (tokens.Current.Type != TokenType.Identifier)
            {
                throw new InvalidOperationException("Expected field name");
            }

            var fieldName = tokens.Current;

            if (!tokens.MoveNext() || tokens.Current.Type != TokenType.Equals)
            {
                throw new InvalidOperationException("Expected =");
            }

            var fieldValue = PopExpression(tokens)
                ?? throw new InvalidOperationException("Expected field initializer value expression");

            fieldInitializers.Add(new FieldInitializer(fieldName, fieldValue));
        }

        return new Expression(new ObjectInitializer(type, fieldInitializers));
    }

    private static Expression GetMethodReturn(PeekableEnumerator<Token> tokens)
    {
        var expression = new MethodReturn(PopExpression(tokens) ?? throw new InvalidOperationException("Expected expression"));

        return new Expression(expression);
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
            previousExpression = MatchTokenToExpression(previousExpression, tokens);
            
            if (!tokens.TryPeek(out peeked) 
                || !TryGetBindingStrength(peeked, out var bindingStrength)
                || bindingStrength <= currentBindingStrength)
            {
                break;
            }
        }

        return previousExpression;
    }

    private static Expression GetMethodCall(PeekableEnumerator<Token> tokens, Expression method)
    {
        var parameterList = new List<Expression>();
        while (tokens.TryPeek(out var peeked))
        {
            if (peeked.Type == TokenType.RightParenthesis)
            {
                tokens.MoveNext();
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

        var checkExpression = PopExpression(tokens)
            ?? throw new InvalidOperationException("Expected check expression, found no expression");

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
            // pop the "else" token off
            tokens.MoveNext();

            if (tokens.TryPeek(out var nextPeeked) && nextPeeked.Type == TokenType.If)
            {
                // pop the "if" token off
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
            checkExpression,
            body,
            elseIfs,
            elseBody));
    }

    private static Expression GetBlockExpression(PeekableEnumerator<Token> tokens)
    {
        var (expressions, functions, classes, fields) = GetScope(tokens, TokenType.RightBrace);

        if (classes.Count > 0)
        {
            throw new InvalidOperationException("Block expressions cannot contain classes");
        }
        if (fields.Count > 0)
        {
            throw new InvalidOperationException("Block expressions cannot contain fields");
        }
        
        return new Expression(new Block(expressions, functions));
    }

    private static Expression GetVariableDeclaration(PeekableEnumerator<Token> tokens)
    {
        if (!tokens.MoveNext())
        {
            throw new InvalidOperationException("Expected variable identifier, got nothing");
        }

        MutabilityModifier? mutabilityModifier = null;

        if (tokens.Current.Type == TokenType.Mut)
        {
            mutabilityModifier = new MutabilityModifier(tokens.Current);

            if (!tokens.MoveNext())
            {
                throw new InvalidOperationException("Expected variable identifier");
            }
        }

        var identifier = tokens.Current;
        if (identifier.Type != TokenType.Identifier)
        {
            throw new InvalidOperationException($"Expected variable identifier, got {identifier}");
        }

        if (!tokens.MoveNext())
        {
            throw new InvalidOperationException("Expected = or ; token, got nothing");
        }

        TypeIdentifier? type = null;
        Expression? valueExpression = null;

        if (tokens.Current.Type == TokenType.Colon)
        {
            if (!tokens.MoveNext())
            {
                throw new InvalidOperationException("Expected type");
            }

            type = GetTypeIdentifier(tokens);

            if (tokens.TryPeek(out var peeked) && peeked.Type == TokenType.Equals)
            {
                tokens.MoveNext();
                valueExpression = PopExpression(tokens)
                            ?? throw new InvalidOperationException("Expected value expression, got nothing");
            }
        }
        else if (tokens.Current.Type == TokenType.Equals)
        {
            valueExpression = PopExpression(tokens)
                        ?? throw new InvalidOperationException("Expected value expression, got nothing");
        }

        return new Expression(new VariableDeclaration(identifier, mutabilityModifier, type, valueExpression));
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
        BinaryOperatorType operatorType)
    {
        var operatorToken = tokens.Current;
        if (!TryGetBindingStrength(operatorToken, out var bindingStrength))
        {
            throw new UnreachableException("All operators have a binding strength");
        }
        var right = PopExpression(tokens, bindingStrength.Value);

        return right is null
            ? throw new Exception("Expected expression but got null")
            : new Expression(new BinaryOperator(operatorType, leftOperand, right.Value, operatorToken));
    }

    private static bool TryGetBindingStrength(Token token, [NotNullWhen(true)] out uint? bindingStrength)
    {
        bindingStrength = token.Type switch
        {
            // unary operators
            TokenType.QuestionMark => GetUnaryOperatorBindingStrength(UnaryOperatorType.FallOut),
            // binary operators
            TokenType.RightAngleBracket => GetBinaryOperatorBindingStrength(BinaryOperatorType.GreaterThan),
            TokenType.LeftAngleBracket => GetBinaryOperatorBindingStrength(BinaryOperatorType.LessThan),
            TokenType.Star => GetBinaryOperatorBindingStrength(BinaryOperatorType.Multiply),
            TokenType.ForwardSlash => GetBinaryOperatorBindingStrength(BinaryOperatorType.Divide),
            TokenType.Plus => GetBinaryOperatorBindingStrength(BinaryOperatorType.Plus),
            TokenType.Dash => GetBinaryOperatorBindingStrength(BinaryOperatorType.Minus),
            TokenType.DoubleEquals => GetBinaryOperatorBindingStrength(BinaryOperatorType.EqualityCheck),
            TokenType.Equals => GetBinaryOperatorBindingStrength(BinaryOperatorType.ValueAssignment),
            TokenType.Dot => GetBinaryOperatorBindingStrength(BinaryOperatorType.MemberAccess),
            TokenType.DoubleColon => GetBinaryOperatorBindingStrength(BinaryOperatorType.StaticMemberAccess),
            // todo: could some of these be converted to real operators?
            TokenType.LeftParenthesis => 7,
            TokenType.Turbofish => 6,
            _ => null
        };

        return bindingStrength.HasValue;
    }

    private static uint GetBinaryOperatorBindingStrength(BinaryOperatorType operatorType)
    {
        return operatorType switch
        {
            BinaryOperatorType.Multiply => 5,
            BinaryOperatorType.Divide => 5,
            BinaryOperatorType.Plus => 4,
            BinaryOperatorType.Minus => 4,
            BinaryOperatorType.GreaterThan => 3,
            BinaryOperatorType.LessThan => 3,
            BinaryOperatorType.EqualityCheck => 2,
            BinaryOperatorType.ValueAssignment => 1,
            BinaryOperatorType.MemberAccess => 9,
            BinaryOperatorType.StaticMemberAccess => 10,
            _ => throw new InvalidEnumArgumentException(nameof(operatorType), (int)operatorType, typeof(BinaryOperatorType))
        };
    }
    
    private static uint GetUnaryOperatorBindingStrength(UnaryOperatorType operatorType)
    {
        return operatorType switch
        {
            UnaryOperatorType.FallOut => 8,
            _ => throw new InvalidEnumArgumentException(nameof(operatorType), (int)operatorType, typeof(UnaryOperatorType))
        };
    }
}