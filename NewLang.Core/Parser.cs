using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace NewLang.Core;

public static class Parser
{
    public static LangProgram Parse(IEnumerable<Token> tokens)
    {
        using var tokensEnumerator = new PeekableEnumerator<Token>(tokens.GetEnumerator());

        var scope = GetScope(tokensEnumerator, closingToken: null, isUnion: false);

        if (scope.GetScopeTypes().Contains(Scope.ScopeType.Field))
        {
            throw new InvalidOperationException("A field is not a valid statement");
        }

        return new LangProgram(scope.Expressions, scope.Functions, scope.Classes, scope.Unions);
    }

    /// <summary>
    /// Get a scope. Expects first token to be the first token within the scope (or the closing token)
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    private static Scope GetScope(
        PeekableEnumerator<Token> tokens,
        TokenType? closingToken,
        bool isUnion)
    {
        var hasTailExpression = false;
        var foundClosingToken = false;

        var expressions = new List<IExpression>();
        var functions = new List<LangFunction>();
        var fields = new List<ClassField>();
        var classes = new List<ProgramClass>();
        var unions = new List<ProgramUnion>();
        var variants = new List<IProgramUnionVariant>();
                
        var expectMemberComma = false;
        var allowMemberComma = false;
        
        while (TryPeek(tokens, out var peeked))
        {
            if (peeked.Type == closingToken)
            {
                // pop the peeked closing token off
                MoveNext(tokens);
                foundClosingToken = true;
                break;
            }

            if (peeked.Type == TokenType.Comma)
            {
                if (!allowMemberComma)
                {
                    throw new InvalidOperationException("Unexpected ,");
                }

                MoveNext(tokens);
                if (!TryPeek(tokens, out peeked))
                {
                    break;
                }
            }
            else if (expectMemberComma)
            {
                throw new InvalidOperationException("Expected comma");
            }
            
            if (peeked.Type == closingToken)
            {
                // pop the peeked closing token off
                MoveNext(tokens);
                foundClosingToken = true;
                break;
            }

            if (IsMember(peeked.Type, isUnion))
            {
                expectMemberComma = false;
                
                MoveNext(tokens);
                var (function, @class, field, union, variant) = GetMember(tokens, isUnion);
                if (function is not null)
                {
                    functions.Add(function);
                }
                else if (@class is not null)
                {
                    classes.Add(@class);
                }
                else if (field is not null)
                {
                    fields.Add(field);

                    expectMemberComma = true;
                }
                else if (union is not null)
                {
                    unions.Add(union);
                }
                else if (variant is not null)
                {
                    expectMemberComma = true;
                    variants.Add(variant);
                }

                allowMemberComma = true;
                continue;
            }

            var expression = PopExpression(tokens);
            if (expression is null)
            {
                // found semicolon, pop semicolon off and continue
                MoveNext(tokens);
                continue;
            }
            
            if (hasTailExpression)
            {
                throw new InvalidOperationException("Tail expression must be at the end of a block");
            }

            if (!TryPeek(tokens, out peeked) || peeked.Type != TokenType.Semicolon)
            {
                if (expression.ExpressionType == ExpressionType.MethodReturn)
                {
                    throw new InvalidOperationException("Return statement cannot be a tail expression");
                }

                hasTailExpression = expression.ExpressionType is not (ExpressionType.IfExpression or ExpressionType.Block);
            }
            else
            {
                // drop semicolon
                MoveNext(tokens);
                if (!IsValidStatement(expression))
                {
                    throw new InvalidOperationException($"{expression.ExpressionType} is not a valid statement");
                }
            } 
            
            expressions.Add(expression);
        }
        
        if (!foundClosingToken && closingToken.HasValue)
        {
            throw new InvalidOperationException($"Expected {closingToken.Value}, got nothing");
        }

        return new Scope
        {
            Classes = classes,
            Expressions = expressions,
            Functions = functions,
            Fields = fields,
            Unions = unions,
            Variants = variants
        };
    }

    private static bool IsMember(TokenType tokenType, bool isUnion)
    {
        return (tokenType is TokenType.Pub or TokenType.Mut or TokenType.Static or TokenType.Fn or TokenType.Class or TokenType.Field or TokenType.Union)
            || (isUnion && tokenType == TokenType.Identifier);
    }

    private static (LangFunction? Function, ProgramClass? Class, ClassField? Field, ProgramUnion? Union, IProgramUnionVariant? Variant) GetMember(
        PeekableEnumerator<Token> tokens, bool isUnion)
    {
        AccessModifier? accessModifier = null;
        MutabilityModifier? mutabilityModifier = null;
        StaticModifier? staticModifier = null;

        if (tokens.Current.Type == TokenType.Pub)
        {
            accessModifier = new AccessModifier(tokens.Current);
            if (!MoveNext(tokens))
            {
                throw new InvalidOperationException("Expected field, mut, static, fn or class");
            }
        }
        
        if (tokens.Current.Type == TokenType.Static)
        {
            staticModifier = new StaticModifier(tokens.Current);
            if (!MoveNext(tokens))
            {
                throw new InvalidOperationException("Expected field, fn or class");
            }
        }
        
        if (tokens.Current.Type == TokenType.Mut)
        {
            mutabilityModifier = new MutabilityModifier(tokens.Current);
            if (!MoveNext(tokens))
            {
                throw new InvalidOperationException("Expected field, fn, static or class");
            }
        }

        if (tokens.Current.Type == TokenType.Fn)
        {
            if (mutabilityModifier is not null)
            {
                throw new InvalidOperationException("Function cannot have mutability modifier");
            }

            var function = GetFunctionDeclaration(accessModifier, staticModifier, tokens);
            return (function, null, null, null, null);
        }

        if (tokens.Current.Type == TokenType.Class)
        {
            if (mutabilityModifier is not null)
            {
                throw new InvalidOperationException("Class cannot have a mutability modifier");
            }

            if (staticModifier is not null)
            {
                throw new InvalidOperationException("Class cannot be static");
            }

            var @class = GetClassDefinition(accessModifier, tokens);

            return (null, @class, null, null, null);
        }

        if (tokens.Current.Type == TokenType.Field)
        {
            var field = GetField(accessModifier, staticModifier, mutabilityModifier, tokens);

            return (null, null, field, null, null);
        }

        if (tokens.Current.Type == TokenType.Union)
        {
            if (mutabilityModifier is not null)
            {
                throw new InvalidOperationException("Union cannot have a mutability modifier");
            }

            if (staticModifier is not null)
            {
                throw new InvalidOperationException("Union cannot be static");
            }
            
            var union = GetUnionDefinition(accessModifier, tokens);

            return (null, null, null, union, null);
        }

        if (tokens.Current is StringToken {Type: TokenType.Identifier} name && isUnion)
        {
            return (null, null, null, null, GetUnionVariant(tokens, name));
        }
        
        throw new InvalidOperationException("Expected class, fn, union, field, variant");
    }

    private static IProgramUnionVariant GetUnionVariant(PeekableEnumerator<Token> tokens, StringToken variantName)
    {
         if (!TryPeek(tokens, out var peeked))
         {
             throw new InvalidOperationException("Expected (, {, ',' or }");
         }

         if (peeked.Type == TokenType.RightBrace)
         {
             // we don't want to remove the right brace, this is the end of the union
             return new UnitStructUnionVariant(variantName);
         }

         if (peeked.Type == TokenType.Comma)
         {
             return new UnitStructUnionVariant(variantName);
         }

         if (peeked.Type == TokenType.LeftParenthesis)
         {
             MoveNext(tokens);
             
             var tupleTypes = new List<TypeIdentifier>();
             while (TryPeek(tokens, out peeked))
             {
                 if (peeked.Type == TokenType.RightParenthesis)
                 {
                     MoveNext(tokens);
                     break;
                 }
     
                 if (tupleTypes.Count > 0)
                 {
                     if (peeked.Type != TokenType.Comma)
                     {
                         throw new InvalidOperationException("Expected comma");
                     }
     
                     // pop comma off
                     MoveNext(tokens);
                 }
     
                 if (TryPeek(tokens, out peeked) && peeked.Type == TokenType.RightParenthesis)
                 {
                     MoveNext(tokens);
                     break;
                 }

                 MoveNext(tokens);

                 var nextType = GetTypeIdentifier(tokens);
                 tupleTypes.Add(nextType);
             }

             return new TupleUnionVariant(variantName, tupleTypes);
         }

         if (peeked.Type == TokenType.LeftBrace)
         {
             MoveNext(tokens);
             var scope = GetScope(tokens, TokenType.RightBrace, isUnion: false);

             if (scope.GetScopeTypes().Any(x => x is not Scope.ScopeType.Field))
             {
                 throw new InvalidOperationException("Struct union variants can only contain fields");
             }

             return new StructUnionVariant
             {
                 Name = variantName,
                 Fields = scope.Fields
             };
         }

         throw new InvalidOperationException($"Unexpected token {peeked}");
    }

    private static ClassField GetField(AccessModifier? accessModifier, StaticModifier? staticModifier, MutabilityModifier? mutabilityModifier, PeekableEnumerator<Token> tokens)
    {
        // pub mut field MyField: string;
        if (!MoveNext(tokens) || tokens.Current is not StringToken { Type: TokenType.Identifier } name)
        {
            throw new InvalidOperationException("Expected field name");
        }

        if (!MoveNext(tokens) || tokens.Current.Type != TokenType.Colon)
        {
            throw new InvalidOperationException("Expected :");
        }

        if (!MoveNext(tokens))
        {
            throw new InvalidOperationException("Expected field type");
        }

        var type = GetTypeIdentifier(tokens);

        IExpression? valueExpression = null;

        if (TryPeek(tokens, out var peeked) && peeked.Type == TokenType.Equals)
        {
            MoveNext(tokens);

            valueExpression = PopExpression(tokens)
                ?? throw new InvalidOperationException("Expected field initializer expression");
        }

        return new ClassField(accessModifier, staticModifier, mutabilityModifier, name, type, valueExpression);
    }

    private static ProgramUnion GetUnionDefinition(AccessModifier? accessModifier, PeekableEnumerator<Token> tokens)
    {
        if (!MoveNext(tokens) || tokens.Current is not StringToken { Type: TokenType.Identifier } name)
        {
            throw new InvalidOperationException("Expected union name");
        }
        
        if (!MoveNext(tokens))
        {
            throw new InvalidOperationException("Expected { or <");
        }
        
        var typeArguments = new List<StringToken>();
        
        if (tokens.Current.Type == TokenType.LeftAngleBracket)
        {
            while (true)
            {
                if (!MoveNext(tokens))
                {
                    throw new InvalidOperationException("Expected Type Argument");
                }

                if (tokens.Current.Type == TokenType.RightAngleBracket)
                {
                    if (typeArguments.Count == 0)
                    {
                        throw new InvalidOperationException("Expected type argument");
                    }

                    if (!MoveNext(tokens))
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

                    if (!MoveNext(tokens))
                    {
                        throw new InvalidOperationException("Expected type argument");
                    }
                }

                if (tokens.Current.Type == TokenType.RightAngleBracket)
                {
                    if (typeArguments.Count == 0)
                    {
                        throw new InvalidOperationException("Expected type argument");
                    }

                    if (!MoveNext(tokens))
                    {
                        throw new InvalidOperationException("Expected {");
                    }
                    break;
                }

                if (tokens.Current is not StringToken { Type: TokenType.Identifier } typeArgument)
                {
                    throw new InvalidOperationException("Expected type argument");
                }

                typeArguments.Add(typeArgument);
            }
        }

        if (tokens.Current.Type != TokenType.LeftBrace)
        {
            throw new InvalidOperationException("Expected {");
        }

        var scope = GetScope(tokens, closingToken: TokenType.RightBrace, isUnion: true);
        if (scope.GetScopeTypes().Any(x => x is not (Scope.ScopeType.Function or Scope.ScopeType.Variant)))
        {
            throw new InvalidOperationException("Unions can only contain functions and variants");
        }

        return new ProgramUnion(
            accessModifier,
            name,
            typeArguments,
            scope.Functions,
            scope.Variants);
    }

    private static ProgramClass GetClassDefinition(AccessModifier? accessModifier, PeekableEnumerator<Token> tokens)
    {
        if (!MoveNext(tokens) || tokens.Current is not StringToken { Type: TokenType.Identifier } name)
        {
            throw new InvalidOperationException("Expected class name");
        }

        if (!MoveNext(tokens))
        {
            throw new InvalidOperationException("Expected { or <");
        }

        var typeArguments = new List<StringToken>();

        if (tokens.Current.Type == TokenType.LeftAngleBracket)
        {
            while (true)
            {
                if (!MoveNext(tokens))
                {
                    throw new InvalidOperationException("Expected Type Argument");
                }

                if (tokens.Current.Type == TokenType.RightAngleBracket)
                {
                    if (typeArguments.Count == 0)
                    {
                        throw new InvalidOperationException("Expected type argument");
                    }

                    if (!MoveNext(tokens))
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

                    if (!MoveNext(tokens))
                    {
                        throw new InvalidOperationException("Expected type argument");
                    }
                }

                if (tokens.Current.Type == TokenType.RightAngleBracket)
                {
                    if (typeArguments.Count == 0)
                    {
                        throw new InvalidOperationException("Expected type argument");
                    }

                    if (!MoveNext(tokens))
                    {
                        throw new InvalidOperationException("Expected {");
                    }
                    break;
                }

                if (tokens.Current is not StringToken { Type: TokenType.Identifier } typeArgument)
                {
                    throw new InvalidOperationException("Expected type argument");
                }

                typeArguments.Add(typeArgument);
            }
        }

        if (tokens.Current.Type != TokenType.LeftBrace)
        {
            throw new InvalidOperationException("Expected {");
        }

        var scope = GetScope(tokens, closingToken: TokenType.RightBrace, isUnion: false);
        if (scope.GetScopeTypes().Any(x => x is not (Scope.ScopeType.Function or Scope.ScopeType.Field)))
        {
            throw new InvalidOperationException("Class can only contain functions and fields");
        }

        return new ProgramClass(accessModifier, name, typeArguments, scope.Functions, scope.Fields);
    }

    private static bool TryPeek(PeekableEnumerator<Token> tokens, [NotNullWhen(true)] out Token? foundToken)
    {
        if (!tokens.TryPeek(out foundToken))
        {
            return false;
        }

        var couldPeek = true;
        while (couldPeek && foundToken!.Type is TokenType.MultiLineComment or TokenType.SingleLineComment)
        {
            // drop the comment and try again
            tokens.MoveNext();
            
            couldPeek = tokens.TryPeek(out foundToken);
        }

        return couldPeek;
    }

    private static bool MoveNext(PeekableEnumerator<Token> tokens)
    {
        if (!tokens.MoveNext())
        {
            return false;
        }

        var hasNext = true;
        
        while (hasNext && tokens.Current.Type is TokenType.MultiLineComment or TokenType.SingleLineComment)
        {
            // drop the comment and try again
            hasNext = tokens.MoveNext();
        }

        return hasNext;
    }
    
    private static LangFunction GetFunctionDeclaration(AccessModifier? accessModifier, StaticModifier? staticModifier, PeekableEnumerator<Token> tokens)
    {
        if (!MoveNext(tokens) || tokens.Current is not StringToken {Type: TokenType.Identifier} nameToken)
        {
            throw new InvalidOperationException("Expected function name");
        }

        if (!MoveNext(tokens))
        {
            throw new InvalidOperationException("Expected ( or <");
        }

        var typeArguments = new List<StringToken>();

        if (tokens.Current.Type == TokenType.LeftAngleBracket)
        {
            while (true)
            {
                if (!MoveNext(tokens))
                {
                    throw new InvalidOperationException("Expected Type Argument");
                }

                if (tokens.Current.Type == TokenType.RightAngleBracket)
                {
                    if (typeArguments.Count == 0)
                    {
                        throw new InvalidOperationException("Expected type argument");
                    }

                    if (!MoveNext(tokens))
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

                    if (!MoveNext(tokens))
                    {
                        throw new InvalidOperationException("Expected type argument");
                    }
                }
                
                if (tokens.Current.Type == TokenType.RightAngleBracket)
                {
                    if (typeArguments.Count == 0)
                    {
                        throw new InvalidOperationException("Expected type argument");
                    }

                    if (!MoveNext(tokens))
                    {
                        throw new InvalidOperationException("Expected (");
                    }
                    break;
                }

                if (tokens.Current is not StringToken { Type: TokenType.Identifier } typeArgument)
                {
                    throw new InvalidOperationException("Expected type argument");
                }

                typeArguments.Add(typeArgument);
            }
        }

        if (tokens.Current.Type != TokenType.LeftParenthesis)
        {
            throw new InvalidOperationException("Expected (");
        }

        var parameterList = new List<FunctionParameter>();

        while (true)
        {
            if (!MoveNext(tokens))
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
                if (!MoveNext(tokens))
                {
                    throw new InvalidOperationException("Expected parameter type");
                }
            }

            if (tokens.Current.Type == TokenType.RightParenthesis)
            {
                break;
            }

            MutabilityModifier? mutabilityModifier = null;

            if (tokens.Current.Type == TokenType.Mut)
            {
                mutabilityModifier = new MutabilityModifier(tokens.Current);
                if (!MoveNext(tokens))
                {
                    throw new InvalidOperationException("Expected parameter name");
                }
            }

            if (tokens.Current is not StringToken { Type: TokenType.Identifier } parameterName)
            {
                throw new InvalidOperationException("Expected parameter name");
            }

            if (!MoveNext(tokens) || tokens.Current.Type != TokenType.Colon)
            {
                throw new InvalidOperationException("Expected :");
            }

            if (!MoveNext(tokens))
            {
                throw new InvalidOperationException("Expected parameter type");
            }
            
            var parameterType = GetTypeIdentifier(tokens);

            parameterList.Add(new FunctionParameter(parameterType, mutabilityModifier, parameterName));
        }

        if (!MoveNext(tokens))
        {
            throw new InvalidOperationException("Expected { or :");
        }

        TypeIdentifier? returnType = null;

        if (tokens.Current.Type == TokenType.Colon)
        {
            if (!MoveNext(tokens))
            {
                throw new InvalidOperationException("Expected return type");
            }

            returnType = GetTypeIdentifier(tokens);

            if (!MoveNext(tokens))
            {
                throw new InvalidOperationException("Expected {");
            }
        }

        var scope = GetScope(tokens, closingToken: TokenType.RightBrace, isUnion: false);

        if (scope.GetScopeTypes().Any(x => x is not (Scope.ScopeType.Expression or Scope.ScopeType.Function)))
        {
            throw new InvalidOperationException("Functions can only contain expressions and local function");
        }

        return new LangFunction(accessModifier, staticModifier, nameToken, typeArguments, parameterList, returnType, new Block(scope.Expressions, scope.Functions));
    }

    private static bool IsTypeTokenType(in TokenType tokenType)
    {
        return tokenType is TokenType.IntKeyword or TokenType.StringKeyword or TokenType.Result or TokenType.Identifier or TokenType.Bool;
    } 

    private static TypeIdentifier GetTypeIdentifier(PeekableEnumerator<Token> tokens)
    {
        if (!IsTypeTokenType(tokens.Current.Type))
        {
            throw new InvalidOperationException("Expected type");
        }

        var typeIdentifier = tokens.Current;

        var typeArguments = new List<TypeIdentifier>();
        if (TryPeek(tokens, out var peeked) && peeked.Type == TokenType.Turbofish)
        {
            if (!MoveNext(tokens) && tokens.Current.Type != TokenType.LeftAngleBracket)
            {
                throw new InvalidOperationException("Expected <");
            }

            while (true)
            {
                if (!MoveNext(tokens))
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
                    if (!MoveNext(tokens))
                    {
                        throw new InvalidOperationException("Expected type argument");
                    }
                }

                if (tokens.Current.Type == TokenType.RightAngleBracket)
                {
                    break;
                }

                typeArguments.Add(GetTypeIdentifier(tokens));
            }
        }

        return new TypeIdentifier(typeIdentifier, typeArguments);
    }

    private static bool IsValidStatement(IExpression expression)
    {
        return expression is
            {
                ExpressionType: ExpressionType.Block
                or ExpressionType.IfExpression
                or ExpressionType.VariableDeclaration
                or ExpressionType.MethodCall
                or ExpressionType.MethodReturn
            } or
            BinaryOperatorExpression {
                BinaryOperator.OperatorType: BinaryOperatorType.ValueAssignment
            };
    }
    
    private static IExpression MatchTokenToExpression(IExpression? previousExpression, PeekableEnumerator<Token> tokens)
    {
        return tokens.Current.Type switch
        {
            // value accessors
            TokenType.StringLiteral or TokenType.IntLiteral or TokenType.True or TokenType.False => new ValueAccessorExpression(
                new ValueAccessor(ValueAccessType.Literal, tokens.Current)),
            TokenType.Var => GetVariableDeclaration(tokens),
            TokenType.LeftBrace => GetBlockExpression(tokens),
            TokenType.If => GetIfExpression(tokens),
            TokenType.LeftParenthesis => GetMethodCall(tokens, previousExpression ?? throw new InvalidOperationException($"Unexpected token {tokens.Current}")),
            TokenType.Return => GetMethodReturn(tokens),
            TokenType.New => GetInitializer(tokens),
            TokenType.Semicolon => throw new UnreachableException("PopExpression should have handled semicolon"),
            TokenType.Dot => GetMemberAccess(tokens, previousExpression ?? throw new InvalidOperationException($"Unexpected token {tokens.Current}")),
            TokenType.DoubleColon => GetStaticMemberAccess(tokens, previousExpression ?? throw new InvalidOperationException($"Unexpected token {tokens.Current}")),
            TokenType.Ok or TokenType.Error => GetVariableAccess(tokens),
            TokenType.Turbofish => GetGenericInstantiation(tokens, previousExpression ?? throw new InvalidOperationException($"Unexpected token {tokens.Current}")),
            _ when IsTypeTokenType(tokens.Current.Type) => GetVariableAccess(tokens),
            _ when TryGetUnaryOperatorType(tokens.Current.Type, out var unaryOperatorType) => GetUnaryOperatorExpression(previousExpression ?? throw new InvalidOperationException($"Unexpected token {tokens.Current}"), tokens.Current, unaryOperatorType.Value),
            _ when TryGetBinaryOperatorType(tokens.Current.Type, out var binaryOperatorType) => GetBinaryOperatorExpression(tokens, previousExpression ?? throw new InvalidOperationException($"Unexpected token {tokens.Current}"), binaryOperatorType.Value),
            _ => throw new InvalidOperationException($"Token type {tokens.Current.Type} not supported")
        };
    }

    private static GenericInstantiationExpression GetGenericInstantiation(PeekableEnumerator<Token> tokens,
        IExpression previousExpression)
    {
        var typeArguments = new List<TypeIdentifier>();
        while (true)
        {
            if (!MoveNext(tokens))
            {
                throw new InvalidOperationException("Expected type argument");
            }
    
            if (tokens.Current.Type == TokenType.RightAngleBracket)
            {
                break;
            }
    
            if (typeArguments.Count > 0)
            {
                if (tokens.Current.Type != TokenType.Comma)
                {
                    throw new InvalidOperationException("Expected ,");
                }
    
                if (!MoveNext(tokens))
                {
                    throw new InvalidOperationException("Expected type argument");
                }
            }

            if (tokens.Current.Type == TokenType.RightAngleBracket)
            {
                break;
            }
    
            typeArguments.Add(GetTypeIdentifier(tokens));
        }

        return new GenericInstantiationExpression(new GenericInstantiation(previousExpression, typeArguments));
    }

    private static ValueAccessorExpression GetVariableAccess(PeekableEnumerator<Token> tokens)
    {
        var variableToken = tokens.Current;
        return new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, variableToken));
    }

    private static StaticMemberAccessExpression GetStaticMemberAccess(
        PeekableEnumerator<Token> tokens,
        IExpression previousExpression)
    {
        IReadOnlyList<TypeIdentifier>? typeArguments = null;
        if (previousExpression is GenericInstantiationExpression genericInstantiationExpression)
        {
            typeArguments = genericInstantiationExpression.GenericInstantiation.GenericArguments;
            previousExpression = genericInstantiationExpression.GenericInstantiation.Value;
        }
        
        if (previousExpression is not ValueAccessorExpression
            {
                ValueAccessor:
                {
                    AccessType: ValueAccessType.Variable, Token: var token
                }
            })
        {
            throw new InvalidOperationException($"Cannot perform static member access on {previousExpression}");
        }

        var type = new TypeIdentifier(token, typeArguments ?? []);

        if (!MoveNext(tokens) || tokens.Current is not StringToken { Type: TokenType.Identifier } memberName)
        {
            throw new InvalidOperationException($"Unexpected token {tokens.Current}");
        }

        return new StaticMemberAccessExpression(new StaticMemberAccess(type, memberName));
    }

    private static MemberAccessExpression GetMemberAccess(PeekableEnumerator<Token> tokens, IExpression previousExpression)
    {
        if (!MoveNext(tokens) || tokens.Current is not StringToken { Type: TokenType.Identifier } memberName)
        {
            throw new InvalidOperationException("Expected member name");
        }

        return new MemberAccessExpression(new MemberAccess(previousExpression, memberName));
    }

    private static IExpression GetInitializer(PeekableEnumerator<Token> tokens)
    {
        if (!MoveNext(tokens))
        {
            throw new InvalidOperationException("Expected type");
        }

        var type = GetTypeIdentifier(tokens);

        if (!MoveNext(tokens))
        {
            throw new InvalidOperationException("Expected :: or {");
        }

        if (tokens.Current.Type == TokenType.LeftBrace)
        {
            return GetObjectInitializer(type, tokens);
        }
        if (tokens.Current.Type == TokenType.DoubleColon)
        {
            return GetUnionStructVariantInitializer(type, tokens);
        }
        
        throw new InvalidOperationException($"Unexpected token {tokens.Current}. Expected :: or {{");
    }

    private static UnionStructVariantInitializerExpression GetUnionStructVariantInitializer(
        TypeIdentifier type,
        PeekableEnumerator<Token> tokens)
    {
        if (!MoveNext(tokens) || tokens.Current is not StringToken { Type: TokenType.Identifier } variantName)
        {
            throw new InvalidOperationException("Expected union variant name");
        }

        if (!MoveNext(tokens) || tokens.Current.Type != TokenType.LeftBrace)
        {
            throw new InvalidOperationException("Expected {");
        }
        
        return new UnionStructVariantInitializerExpression(new UnionStructVariantInitializer(type, variantName, GetFieldInitializers(tokens)));
    }

    private static List<FieldInitializer> GetFieldInitializers(PeekableEnumerator<Token> tokens)
    {
        var fieldInitializers = new List<FieldInitializer>();
        
        while (true)
        {
            if (!MoveNext(tokens))
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

                if (!MoveNext(tokens))
                {
                    throw new InvalidOperationException("Expected field name");
                }
            }

            if (tokens.Current.Type == TokenType.RightBrace)
            {
                break;
            }

            if (tokens.Current is not StringToken { Type: TokenType.Identifier } fieldName)
            {
                throw new InvalidOperationException("Expected field name");
            }

            if (!MoveNext(tokens) || tokens.Current.Type != TokenType.Equals)
            {
                throw new InvalidOperationException("Expected =");
            }

            var fieldValue = PopExpression(tokens)
                ?? throw new InvalidOperationException("Expected field initializer value expression");

            fieldInitializers.Add(new FieldInitializer(fieldName, fieldValue));
        }
        
        return fieldInitializers;
    }

    private static ObjectInitializerExpression GetObjectInitializer(TypeIdentifier type, PeekableEnumerator<Token> tokens)
    {
        return new ObjectInitializerExpression(new ObjectInitializer(type, GetFieldInitializers(tokens)));
    }

    private static MethodReturnExpression GetMethodReturn(PeekableEnumerator<Token> tokens)
    {
        var expression = new MethodReturn(PopExpression(tokens));

        return new MethodReturnExpression(expression);
    }

    public static IExpression? PopExpression(IEnumerable<Token> tokens)
    {
        using var enumerator = new PeekableEnumerator<Token>(tokens.GetEnumerator());

        return PopExpression(enumerator);
    }

    private static IExpression? PopExpression(PeekableEnumerator<Token> tokens, uint? currentBindingStrength = null)
    {
        IExpression? previousExpression = null;
        while (TryPeek(tokens, out var peeked) 
               && peeked.Type != TokenType.Semicolon)
        {
            MoveNext(tokens);
            previousExpression = MatchTokenToExpression(previousExpression, tokens);
            
            if (!TryPeek(tokens, out peeked) 
                || !TryGetBindingStrength(peeked, out var bindingStrength)
                || bindingStrength <= currentBindingStrength)
            {
                break;
            }
        }

        return previousExpression;
    }

    private static MethodCallExpression GetMethodCall(PeekableEnumerator<Token> tokens, IExpression method)
    {
        var parameterList = new List<IExpression>();
        while (TryPeek(tokens, out var peeked))
        {
            if (peeked.Type == TokenType.RightParenthesis)
            {
                MoveNext(tokens);
                return new MethodCallExpression(new MethodCall(method, parameterList));
            }

            if (parameterList.Count > 0)
            {
                if (peeked.Type != TokenType.Comma)
                {
                    throw new InvalidOperationException("Expected comma");
                }

                // pop comma off
                MoveNext(tokens);
            }

            if (TryPeek(tokens, out peeked) && peeked.Type == TokenType.RightParenthesis)
            {
                MoveNext(tokens);
                return new MethodCallExpression(new MethodCall(method, parameterList));
            }

            var nextExpression = PopExpression(tokens);
            if (nextExpression is not null)
            {
                parameterList.Add(nextExpression);
            }
        }

        throw new InvalidOperationException("Expected ), found nothing");
    }

    private static IfExpressionExpression GetIfExpression(PeekableEnumerator<Token> tokens)
    {
        if (!MoveNext(tokens))
        {
            throw new InvalidOperationException("Expected left parenthesis, found nothing");
        }

        if (tokens.Current.Type != TokenType.LeftParenthesis)
        {
            throw new InvalidOperationException($"Expected left parenthesis, found {tokens.Current.Type}");
        }

        var checkExpression = PopExpression(tokens)
            ?? throw new InvalidOperationException("Expected check expression, found no expression");

        if (!MoveNext(tokens))
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
        IExpression? elseBody = null;

        while (TryPeek(tokens, out var peeked) && peeked.Type == TokenType.Else)
        {
            // pop the "else" token off
            MoveNext(tokens);

            if (TryPeek(tokens, out var nextPeeked) && nextPeeked.Type == TokenType.If)
            {
                // pop the "if" token off
                MoveNext(tokens);
                if (!MoveNext(tokens) || tokens.Current.Type != TokenType.LeftParenthesis)
                {
                    throw new InvalidOperationException("Expected left Parenthesis");
                }

                var elseIfCheckExpression = PopExpression(tokens)
                                            ?? throw new InvalidOperationException("Expected check expression");

                if (!MoveNext(tokens) || tokens.Current.Type != TokenType.RightParenthesis)
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

        return new IfExpressionExpression(new IfExpression(
            checkExpression,
            body,
            elseIfs,
            elseBody));
    }

    private static BlockExpression GetBlockExpression(PeekableEnumerator<Token> tokens)
    {
        var scope = GetScope(tokens, TokenType.RightBrace, isUnion: false);

        if (scope.GetScopeTypes().Any(x => x is not (Scope.ScopeType.Expression or Scope.ScopeType.Function)))
        {
            throw new InvalidOperationException("Block expressions can only contain expressions and functions");
        }
        
        return new BlockExpression(new Block(scope.Expressions, scope.Functions));
    }

    private static VariableDeclarationExpression GetVariableDeclaration(PeekableEnumerator<Token> tokens)
    {
        if (!MoveNext(tokens))
        {
            throw new InvalidOperationException("Expected variable identifier, got nothing");
        }

        MutabilityModifier? mutabilityModifier = null;

        if (tokens.Current.Type == TokenType.Mut)
        {
            mutabilityModifier = new MutabilityModifier(tokens.Current);

            if (!MoveNext(tokens))
            {
                throw new InvalidOperationException("Expected variable identifier");
            }
        }

        if (tokens.Current is not StringToken { Type: TokenType.Identifier } identifier)
        {
            throw new InvalidOperationException($"Expected variable identifier, got {tokens.Current}");
        }

        if (!MoveNext(tokens))
        {
            throw new InvalidOperationException("Expected = or ; token, got nothing");
        }

        TypeIdentifier? type = null;
        IExpression? valueExpression = null;

        if (tokens.Current.Type == TokenType.Colon)
        {
            if (!MoveNext(tokens))
            {
                throw new InvalidOperationException("Expected type");
            }

            type = GetTypeIdentifier(tokens);

            if (TryPeek(tokens, out var peeked) && peeked.Type == TokenType.Equals)
            {
                MoveNext(tokens);
                valueExpression = PopExpression(tokens)
                            ?? throw new InvalidOperationException("Expected value expression, got nothing");
            }
        }
        else if (tokens.Current.Type == TokenType.Equals)
        {
            valueExpression = PopExpression(tokens)
                        ?? throw new InvalidOperationException("Expected value expression, got nothing");
        }

        return new VariableDeclarationExpression(new VariableDeclaration(identifier, mutabilityModifier, type, valueExpression));
    }
    
    private static UnaryOperatorExpression GetUnaryOperatorExpression(
        IExpression operand,
        Token operatorToken,
        UnaryOperatorType operatorType)
    {
        return new UnaryOperatorExpression(new UnaryOperator(operatorType, operand, operatorToken));
    }
    
    private static BinaryOperatorExpression GetBinaryOperatorExpression(
        PeekableEnumerator<Token> tokens,
        IExpression leftOperand,
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
            : new BinaryOperatorExpression(new BinaryOperator(operatorType, leftOperand, right, operatorToken));
    }

    private static bool TryGetBindingStrength(Token token, [NotNullWhen(true)] out uint? bindingStrength)
    {
        bindingStrength = token.Type switch
        {
            _ when TryGetUnaryOperatorType(token.Type, out var unaryOperatorType) => GetUnaryOperatorBindingStrength(unaryOperatorType.Value),
            _ when TryGetBinaryOperatorType(token.Type, out var binaryOperatorType) => GetBinaryOperatorBindingStrength(binaryOperatorType.Value),
            TokenType.LeftParenthesis => 7,
            TokenType.Turbofish => 6,
            TokenType.Dot => 9,
            TokenType.DoubleColon => 10,
            _ => null
        };

        return bindingStrength.HasValue;
    }

    private static bool TryGetUnaryOperatorType(TokenType type, [NotNullWhen(true)] out UnaryOperatorType? operatorType)
    {
        operatorType = type switch
        {
            TokenType.QuestionMark => UnaryOperatorType.FallOut,
            _ => null
        };

        return operatorType.HasValue;
    }

    private static bool TryGetBinaryOperatorType(TokenType type, [NotNullWhen(true)] out BinaryOperatorType? operatorType)
    {
        operatorType = type switch
        {
            TokenType.RightAngleBracket => BinaryOperatorType.GreaterThan,
            TokenType.LeftAngleBracket => BinaryOperatorType.LessThan,
            TokenType.Star => BinaryOperatorType.Multiply,
            TokenType.ForwardSlash => BinaryOperatorType.Divide,
            TokenType.Plus => BinaryOperatorType.Plus,
            TokenType.Dash => BinaryOperatorType.Minus,
            TokenType.DoubleEquals => BinaryOperatorType.EqualityCheck,
            TokenType.Equals => BinaryOperatorType.ValueAssignment,
            _ => null
        };

        return operatorType.HasValue;
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

    private class Scope
    {
         public required IReadOnlyList<IExpression> Expressions { get; init; }
         public required IReadOnlyList<LangFunction> Functions { get; init; }
         public required IReadOnlyList<ProgramClass> Classes { get; init; }
         public required IReadOnlyList<ClassField> Fields { get; init; }
         public required IReadOnlyList<ProgramUnion> Unions { get; init; }
         public required IReadOnlyList<IProgramUnionVariant> Variants { get; init; }

         public List<ScopeType> GetScopeTypes()
         {
             List<ScopeType> types = [];
             if (Expressions.Count > 0)
                 types.Add(ScopeType.Expression);
             if (Functions.Count > 0)
                 types.Add(ScopeType.Function);
             if (Classes.Count > 0)
                 types.Add(ScopeType.Class);
             if (Fields.Count > 0)
                 types.Add(ScopeType.Field);
             if (Unions.Count > 0)
                 types.Add(ScopeType.Union);
             if (Variants.Count > 0)
                 types.Add(ScopeType.Variant);

             return types;
         }
         
         public enum ScopeType
         {
             Expression,
             Function,
             Class,
             Field,
             Union,
             Variant
         }
    }
}