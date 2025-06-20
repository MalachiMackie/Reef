using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace NewLang.Core;

public sealed class Parser : IDisposable
{
    public static LangProgram Parse(IEnumerable<Token> tokens)
    {
        using var parser = new Parser(tokens);

        return parser.ParseInner();
    }
    
    /// <summary>
    /// static entry point for a single expression. Used for testing
    /// </summary>
    /// <returns></returns>
    public static IExpression? PopExpression(IEnumerable<Token> tokens)
    {
        using var parser = new Parser(tokens);

        if (!parser.MoveNext())
        {
            return null;
        }
        
        return parser.PopExpression();
    }

    private Parser(IEnumerable<Token> tokens)
    {
        _tokens = tokens.GetEnumerator();
    }

    public void Dispose()
    {
        _tokens.Dispose();
    }

    private readonly IEnumerator<Token> _tokens;
    private Token Current => _tokens.Current;
    
    private bool _hasNext;

    private bool MoveNext()
    {
        if (!_tokens.MoveNext())
        {
            _hasNext = false;
            return false;
        }

        var hasNext = true;
        
        while (hasNext && _tokens.Current.Type is TokenType.MultiLineComment or TokenType.SingleLineComment)
        {
            // drop the comment and try again
            hasNext = _tokens.MoveNext();
        }

        _hasNext = hasNext;

        return hasNext;
    }
    
    private LangProgram ParseInner()
    {
        if (!MoveNext())
        {
            return new LangProgram([], [], [], []);
        }
        
        var scope = GetScope(closingToken: null, isUnion: false);

        if (scope.GetScopeTypes().Contains(Scope.ScopeType.Field))
        {
            throw new InvalidOperationException("A field is not a valid statement");
        }

        return new LangProgram(scope.Expressions, scope.Functions, scope.Classes, scope.Unions);
    }

    /// <summary>
    /// Get a scope. Expects first token to be the token that opened the scope, or the first token in the scope when no token opened the scope
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    private Scope GetScope(
        TokenType? closingToken,
        bool isUnion)
    {
        // if there's a closing token, then we're expecting 
        if (closingToken.HasValue && !MoveNext())
        {
            throw new InvalidOperationException("Unexpected EOF");
        }
        
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

        do
        {
            if (Current.Type == TokenType.Comma)
            {
                if (!allowMemberComma)
                {
                    throw new InvalidOperationException("Unexpected ,");
                }

                expectMemberComma = false;

                if (!MoveNext())
                {
                    break;
                }
            }
            
            if (Current.Type == closingToken)
            {
                MoveNext();
                foundClosingToken = true;
                break;
            }
            
            if (Current.Type != TokenType.Comma && expectMemberComma)
            {
                throw new InvalidOperationException("Expected ,");
            }

            if (IsMember(Current.Type, isUnion))
            {
                var (function, @class, field, union, variant) = GetMember(isUnion);
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

                if (!_hasNext)
                {
                    if (closingToken.HasValue)
                    {
                        throw new InvalidOperationException($"Expected {closingToken}");
                    }
                    break;
                }

                allowMemberComma = true;
                continue;
            }

            var expression = PopExpression();
            if (expression is null)
            {
                if (!MoveNext())
                {
                    break;
                }

                continue;
            }

            if (hasTailExpression)
            {
                throw new InvalidOperationException("Tail expression must be at the end of a block");
            }

            if (_hasNext && Current.Type == TokenType.Semicolon)
            {
                // drop semicolon
                MoveNext();
                if (!IsValidStatement(expression))
                {
                    throw new InvalidOperationException($"{expression.ExpressionType} is not a valid statement");
                }
            }
            else
            {
                if (expression.ExpressionType == ExpressionType.MethodReturn)
                {
                    throw new InvalidOperationException("Return statement cannot be a tail expression");
                }

                hasTailExpression =
                    expression.ExpressionType is not (ExpressionType.IfExpression or ExpressionType.Block);
            }
            expressions.Add(expression);

        } while (_hasNext);
        
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
        return tokenType is TokenType.Pub or TokenType.Mut or TokenType.Static or TokenType.Fn or TokenType.Class or TokenType.Field or TokenType.Union
            || (isUnion && tokenType == TokenType.Identifier);
    }

    private (LangFunction? Function, ProgramClass? Class, ClassField? Field, ProgramUnion? Union, IProgramUnionVariant? Variant) GetMember(
        bool isUnion)
    {
        AccessModifier? accessModifier = null;
        MutabilityModifier? mutabilityModifier = null;
        StaticModifier? staticModifier = null;

        if (Current.Type == TokenType.Pub)
        {
            accessModifier = new AccessModifier(Current);
            if (!MoveNext())
            {
                throw new InvalidOperationException("Expected field, mut, static, fn or class");
            }
        }
        
        if (Current.Type == TokenType.Static)
        {
            staticModifier = new StaticModifier(Current);
            if (!MoveNext())
            {
                throw new InvalidOperationException("Expected field, fn or class");
            }
        }
        
        if (Current.Type == TokenType.Mut)
        {
            mutabilityModifier = new MutabilityModifier(Current);
            if (!MoveNext())
            {
                throw new InvalidOperationException("Expected field, fn, static or class");
            }
        }

        if (Current.Type == TokenType.Fn)
        {
            if (mutabilityModifier is not null)
            {
                throw new InvalidOperationException("Function cannot have mutability modifier");
            }

            var function = GetFunctionDeclaration(accessModifier, staticModifier);
            return (function, null, null, null, null);
        }

        if (Current.Type == TokenType.Class)
        {
            if (mutabilityModifier is not null)
            {
                throw new InvalidOperationException("Class cannot have a mutability modifier");
            }

            if (staticModifier is not null)
            {
                throw new InvalidOperationException("Class cannot be static");
            }

            var @class = GetClassDefinition(accessModifier);

            return (null, @class, null, null, null);
        }

        if (Current.Type == TokenType.Field)
        {
            var field = GetField(accessModifier, staticModifier, mutabilityModifier);

            return (null, null, field, null, null);
        }

        if (Current.Type == TokenType.Union)
        {
            if (mutabilityModifier is not null)
            {
                throw new InvalidOperationException("Union cannot have a mutability modifier");
            }

            if (staticModifier is not null)
            {
                throw new InvalidOperationException("Union cannot be static");
            }
            
            var union = GetUnionDefinition(accessModifier);

            return (null, null, null, union, null);
        }

        if (Current is StringToken {Type: TokenType.Identifier} name && isUnion)
        {
            return (null, null, null, null, GetUnionVariant(name));
        }
        
        throw new InvalidOperationException("Expected class, fn, union, field, variant");
    }

    private IProgramUnionVariant GetUnionVariant(StringToken variantName)
    {
         if (!MoveNext())
         {
             return new UnitStructUnionVariant(variantName);
         }

         if (Current.Type == TokenType.LeftParenthesis)
         {
             var tupleTypes = GetCommaSeparatedList(
                 TokenType.RightParenthesis,
                 "Tuple Variant Parameter Type",
                 GetTypeIdentifier);
             
             return new TupleUnionVariant(variantName, tupleTypes);
         }

         if (Current.Type == TokenType.LeftBrace)
         {
             var scope = GetScope(TokenType.RightBrace, isUnion: false);

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

         return new UnitStructUnionVariant(variantName);
    }

    private ClassField GetField(AccessModifier? accessModifier, StaticModifier? staticModifier, MutabilityModifier? mutabilityModifier)
    {
        // pub mut field MyField: string;
        if (!MoveNext() || Current is not StringToken { Type: TokenType.Identifier } name)
        {
            throw new InvalidOperationException("Expected field name");
        }

        if (!MoveNext() || Current.Type != TokenType.Colon)
        {
            throw new InvalidOperationException("Expected :");
        }

        if (!MoveNext())
        {
            throw new InvalidOperationException("Expected field type");
        }

        var type = GetTypeIdentifier();

        IExpression? valueExpression = null;

        if (_hasNext && Current.Type == TokenType.Equals)
        {
            if (!MoveNext())
            {
                throw new InvalidOperationException("Expected field initializer expression");
            }
            
            valueExpression = PopExpression()
                ?? throw new InvalidOperationException("Expected field initializer expression");
        }

        return new ClassField(accessModifier, staticModifier, mutabilityModifier, name, type, valueExpression);
    }

    private List<StringToken> GetGenericParameterList()
    {
        return GetCommaSeparatedList(
            TokenType.RightAngleBracket,
            "Type Argument",
            () =>
            {
                var result = Current is not StringToken { Type: TokenType.Identifier } typeArgument
                    ? null
                    : typeArgument;

                if (!MoveNext())
                {
                    throw new InvalidOperationException("Expected >, ',' or Type Argument");
                }

                return result;
            });
    }

    private ProgramUnion GetUnionDefinition(AccessModifier? accessModifier)
    {
        if (!MoveNext() || Current is not StringToken { Type: TokenType.Identifier } name)
        {
            throw new InvalidOperationException("Expected union name");
        }
        
        if (!MoveNext())
        {
            throw new InvalidOperationException("Expected { or <");
        }
        
        var typeArguments = new List<StringToken>();
        
        if (Current.Type == TokenType.LeftAngleBracket)
        {
            typeArguments = GetGenericParameterList();

            if (!_hasNext)
            {
                throw new InvalidOperationException("Expected {");
            }
        }

        if (Current.Type != TokenType.LeftBrace)
        {
            throw new InvalidOperationException("Expected {");
        }

        var scope = GetScope(closingToken: TokenType.RightBrace, isUnion: true);
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

    private ProgramClass GetClassDefinition(AccessModifier? accessModifier)
    {
        if (!MoveNext() || Current is not StringToken { Type: TokenType.Identifier } name)
        {
            throw new InvalidOperationException("Expected class name");
        }

        if (!MoveNext())
        {
            throw new InvalidOperationException("Expected { or <");
        }

        var typeArguments = new List<StringToken>();

        if (Current.Type == TokenType.LeftAngleBracket)
        {
            typeArguments = GetGenericParameterList() is { Count: > 0} x
                ? x
                : throw new InvalidOperationException("Expected Type Argument");

            if (!_hasNext)
            {
                throw new InvalidOperationException("{");
            }
        }

        if (Current.Type != TokenType.LeftBrace)
        {
            throw new InvalidOperationException("Expected {");
        }

        var scope = GetScope(closingToken: TokenType.RightBrace, isUnion: false);
        if (scope.GetScopeTypes().Any(x => x is not (Scope.ScopeType.Function or Scope.ScopeType.Field)))
        {
            throw new InvalidOperationException("Class can only contain functions and fields");
        }

        return new ProgramClass(accessModifier, name, typeArguments, scope.Functions, scope.Fields);
    }
    
    private LangFunction GetFunctionDeclaration(AccessModifier? accessModifier, StaticModifier? staticModifier)
    {
        if (!MoveNext() || Current is not StringToken {Type: TokenType.Identifier} nameToken)
        {
            throw new InvalidOperationException("Expected function name");
        }

        if (!MoveNext())
        {
            throw new InvalidOperationException("Expected ( or <");
        }

        var typeArguments = new List<StringToken>();

        if (Current.Type == TokenType.LeftAngleBracket)
        {
            typeArguments = GetGenericParameterList() is { Count: > 0 } x
                ? x
                : throw new InvalidOperationException("Expected Type Argument");

            if (!_hasNext)
            {
                throw new InvalidCastException("Expected (");
            }
        }

        if (Current.Type != TokenType.LeftParenthesis)
        {
            throw new InvalidOperationException("Expected (");
        }

        var parameterList = GetCommaSeparatedList(
            TokenType.RightParenthesis,
            "Parameter",
            () =>
            {
                MutabilityModifier? mutabilityModifier = null;
            
                if (Current.Type == TokenType.Mut)
                {
                    mutabilityModifier = new MutabilityModifier(Current);
                    if (!MoveNext())
                    {
                        throw new InvalidOperationException("Expected parameter name");
                    }
                }    
                
                if (Current is not StringToken { Type: TokenType.Identifier } parameterName)
                {
                    throw new InvalidOperationException("Expected parameter name");
                }
                
                if (!MoveNext() || Current.Type != TokenType.Colon)
                {
                    throw new InvalidOperationException("Expected :");
                }
                if (!MoveNext())
                {
                    throw new InvalidOperationException("Expected parameter type");
                }
                
                var parameterType = GetTypeIdentifier();
                return new FunctionParameter(parameterType, mutabilityModifier, parameterName);
            });

        TypeIdentifier? returnType = null;

        if (!_hasNext)
        {
            throw new InvalidOperationException("Expected : or {");
        }

        if (Current.Type == TokenType.Colon)
        {
            if (!MoveNext())
            {
                throw new InvalidOperationException("Expected return type");
            }

            returnType = GetTypeIdentifier();

            if (!_hasNext)
            {
                throw new InvalidOperationException("Expected {");
            }
        }

        if (Current.Type != TokenType.LeftBrace)
        {
            throw new InvalidOperationException("Expected {");
        }

        var scope = GetScope(closingToken: TokenType.RightBrace, isUnion: false);

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

    private TypeIdentifier GetTypeIdentifier()
    {
        if (!IsTypeTokenType(Current.Type))
        {
            throw new InvalidOperationException("Expected type");
        }

        var typeIdentifier = Current;

        var typeArguments = new List<TypeIdentifier>();
        if (MoveNext() && Current.Type == TokenType.Turbofish)
        {
            typeArguments = GetCommaSeparatedList(
                TokenType.RightAngleBracket,
                "Type Argument",
                GetTypeIdentifier);
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
    
    private IExpression MatchTokenToExpression(IExpression? previousExpression)
    {
        return Current.Type switch
        {
            // value accessors
            TokenType.StringLiteral or TokenType.IntLiteral or TokenType.True or TokenType.False => GetLiteralExpression(),
            TokenType.Var => GetVariableDeclaration(),
            TokenType.LeftBrace => GetBlockExpression(),
            TokenType.If => GetIfExpression(),
            TokenType.LeftParenthesis => GetMethodCall(previousExpression ?? throw new InvalidOperationException($"Unexpected token {Current}")),
            TokenType.Return => GetMethodReturn(),
            TokenType.New => GetInitializer(),
            TokenType.Semicolon => throw new UnreachableException("PopExpression should have handled semicolon"),
            TokenType.Dot => GetMemberAccess(previousExpression ?? throw new InvalidOperationException($"Unexpected token {Current}")),
            TokenType.DoubleColon => GetStaticMemberAccess(previousExpression ?? throw new InvalidOperationException($"Unexpected token {Current}")),
            TokenType.Ok or TokenType.Error => GetVariableAccess(),
            TokenType.Turbofish => GetGenericInstantiation(previousExpression ?? throw new InvalidOperationException($"Unexpected token {Current}")),
            TokenType.Matches => GetMatchesExpression(previousExpression ?? throw new InvalidOperationException($"Unexpected token {Current}")),
            _ when IsTypeTokenType(Current.Type) => GetVariableAccess(),
            _ when TryGetUnaryOperatorType(Current.Type, out var unaryOperatorType) => GetUnaryOperatorExpression(previousExpression ?? throw new InvalidOperationException($"Unexpected token {Current}"), Current, unaryOperatorType.Value),
            _ when TryGetBinaryOperatorType(Current.Type, out var binaryOperatorType) => GetBinaryOperatorExpression(previousExpression ?? throw new InvalidOperationException($"Unexpected token {Current}"), binaryOperatorType.Value),
            _ => throw new InvalidOperationException($"Token type {Current.Type} not supported")
        };
    }

    private MatchesExpression GetMatchesExpression(IExpression previousExpression)
    {
        if (!MoveNext())
        {
            throw new InvalidOperationException("Expected pattern");
        }
        
        var pattern = GetPattern();

        return new MatchesExpression(previousExpression, pattern);
    }

    private IPattern GetPattern()
    {
        if (Current.Type == TokenType.Underscore)
        {
            MoveNext();

            return new DiscardPattern();
        }

        if (Current.Type == TokenType.Var)
        {
            if (!MoveNext() || Current is not StringToken { Type: TokenType.Identifier } variableName)
            {
                throw new InvalidOperationException("Expected variable name");
            }

            MoveNext();

            return new VariableDeclarationPattern(variableName);
        }

        if (!IsTypeTokenType(Current.Type))
        {
            throw new InvalidOperationException("Expected pattern");
        }

        var type = GetTypeIdentifier();
        
        if (!_hasNext)
        {
            return new ClassPattern(type, [], false, null);
        }

        StringToken? variantName = null;
        
        if (Current.Type == TokenType.DoubleColon)
        {
            if (!MoveNext() || Current is not StringToken { Type: TokenType.Identifier } x)
            {
                throw new InvalidOperationException("Expected union variant name");
            }

            variantName = x;

            MoveNext();
        }

        if (!_hasNext && variantName is not null)
        {
            return new UnionVariantPattern(type, variantName, null);
        }

        if (Current.Type == TokenType.Var)
        {
            if (!MoveNext() || Current is not StringToken { Type: TokenType.Identifier } variableName)
            {
                throw new InvalidOperationException("Expected variable name");
            }

            MoveNext();

            return variantName is null
                ? new ClassPattern(type, [], false, variableName)
                : new UnionVariantPattern(type, variantName, variableName);
        }

        if (variantName is not null && Current.Type == TokenType.LeftParenthesis)
        {
            var patterns = GetCommaSeparatedList(
                TokenType.RightParenthesis,
                "Pattern",
                GetPattern);

            StringToken? variableName = null;

            if (_hasNext && Current.Type == TokenType.Var)
            {
                if (!MoveNext() || Current is not StringToken { Type: TokenType.Identifier } x)
                {
                    throw new InvalidOperationException("Expected variable name");
                }

                variableName = x;

                MoveNext();
            }

            return new UnionTupleVariantPattern(type, variantName, patterns, variableName);
        }

        if (Current.Type == TokenType.LeftBrace)
        {
            var fieldPatterns = GetCommaSeparatedList(
                TokenType.RightBrace,
                "Field Pattern",
                () =>
                {
                    if (Current.Type == TokenType.Underscore)
                    {
                        MoveNext();
                        // hack to allow discard without field name
                        return new KeyValuePair<StringToken, IPattern?>(Token.Identifier("", SourceSpan.Default),
                            new DiscardPattern());
                    }

                    if (Current is not StringToken { Type: TokenType.Identifier } fieldName)
                    {
                        throw new InvalidOperationException("Expected field name");
                    }

                    if (!MoveNext())
                    {
                        throw new InvalidOperationException("Expected : or ,");
                    }

                    IPattern? pattern = null;

                    if (Current.Type == TokenType.Colon)
                    {
                        if (!MoveNext())
                        {
                            throw new InvalidOperationException("Expected pattern");
                        }
                        
                        pattern = GetPattern();
                    }

                    return KeyValuePair.Create(fieldName, pattern);
                });

            var discards = fieldPatterns.Where(x => x.Key.StringValue.Length == 0 && x.Value is DiscardPattern)
                .ToArray();

            switch (discards.Length)
            {
                case > 1:
                    throw new InvalidOperationException("Pattern can only have one field discard");
                case 1 when fieldPatterns[^1] is not {Key.StringValue.Length: 0, Value: DiscardPattern}:
                    throw new InvalidOperationException("field discard must be at the end of the pattern");
                default:
                    fieldPatterns = fieldPatterns.Where(x => x.Key.StringValue.Length > 0).ToList();
            
                    return variantName is not null
                        ? new UnionStructVariantPattern(type, variantName, fieldPatterns, discards.Length == 1, null)
                        : new ClassPattern(type, fieldPatterns, discards.Length == 1, null);
            }
        }

        if (variantName is not null)
        {
            return new UnionVariantPattern(type, variantName, null);
        }

        return new ClassPattern(type, [], false, null);
    }

    private ValueAccessorExpression GetLiteralExpression()
    {
        var expression = new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Current));

        MoveNext();

        return expression;
    }

    /// <summary>
    /// Get a comma separated list of items.
    /// Expects Current to be the opening token.
    /// tryGetNext will be performed on the second token.
    /// Current will be left on token after the terminator
    /// </summary>
    /// <param name="terminator"></param>
    /// <param name="itemName"></param>
    /// <param name="tryGetNext"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    private List<T> GetCommaSeparatedList<T>(TokenType terminator, string itemName, Func<T?> tryGetNext)
    {
        if (!MoveNext())
        {
            throw new InvalidOperationException($"Expected {itemName}");
        }
        
        var items = new List<T>();
        while (true)
        {
            if (!_hasNext)
            {
                throw new InvalidOperationException($"Expected {itemName}");
            }
            
            if (Current.Type == terminator)
            {
                break;
            }

            if (items.Count > 0)
            {
                if (Current.Type != TokenType.Comma)
                {
                    throw new InvalidOperationException("Expected ,");
                }

                if (!MoveNext())
                {
                    throw new InvalidOperationException($"Expected {itemName}");
                }
            }
            
            if (Current.Type == terminator)
            {
                break;
            }

            var next = tryGetNext();

            if (next is null)
            {
                throw new InvalidOperationException($"Expected {itemName}");
            }
            
            items.Add(next);
        }

        MoveNext();
        
        return items;
    }

    private GenericInstantiationExpression GetGenericInstantiation(IExpression previousExpression)
    {
        var typeArguments = GetCommaSeparatedList(TokenType.RightAngleBracket, "Type Argument", GetTypeIdentifier);

        return new GenericInstantiationExpression(new GenericInstantiation(previousExpression, typeArguments));
    }

    private ValueAccessorExpression GetVariableAccess()
    {
        var variableToken = Current;

        MoveNext();
        
        return new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, variableToken));
    }

    private StaticMemberAccessExpression GetStaticMemberAccess(
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

        if (!MoveNext() || Current is not StringToken { Type: TokenType.Identifier } memberName)
        {
            throw new InvalidOperationException($"Unexpected token {Current}");
        }

        MoveNext();

        return new StaticMemberAccessExpression(new StaticMemberAccess(type, memberName));
    }

    private MemberAccessExpression GetMemberAccess(IExpression previousExpression)
    {
        if (!MoveNext() || Current is not StringToken { Type: TokenType.Identifier } memberName)
        {
            throw new InvalidOperationException("Expected member name");
        }

        MoveNext();

        return new MemberAccessExpression(new MemberAccess(previousExpression, memberName));
    }

    private IExpression GetInitializer()
    {
        if (!MoveNext())
        {
            throw new InvalidOperationException("Expected type");
        }

        var type = GetTypeIdentifier();

        if (!_hasNext)
        {
            throw new InvalidOperationException("Expected :: or {");
        }

        return Current.Type switch
        {
            TokenType.LeftBrace => GetObjectInitializer(type),
            TokenType.DoubleColon => GetUnionStructVariantInitializer(type),
            _ => throw new InvalidOperationException($"Unexpected token {Current}. Expected :: or {{")
        };
    }

    private UnionStructVariantInitializerExpression GetUnionStructVariantInitializer(
        TypeIdentifier type)
    {
        if (!MoveNext() || Current is not StringToken { Type: TokenType.Identifier } variantName)
        {
            throw new InvalidOperationException("Expected union variant name");
        }

        if (!MoveNext() || Current.Type != TokenType.LeftBrace)
        {
            throw new InvalidOperationException("Expected {");
        }
        
        return new UnionStructVariantInitializerExpression(new UnionStructVariantInitializer(type, variantName, GetFieldInitializers()));
    }

    private List<FieldInitializer> GetFieldInitializers()
    {
        return GetCommaSeparatedList(
            TokenType.RightBrace,
            "Field Initializer",
            () =>
            {
                 if (Current is not StringToken { Type: TokenType.Identifier } fieldName)
                 {
                     throw new InvalidOperationException("Expected field name");
                 }
     
                 if (!MoveNext() || Current.Type != TokenType.Equals)
                 {
                     throw new InvalidOperationException("Expected =");
                 }

                 if (!MoveNext())
                 {
                     throw new InvalidOperationException("Expected field initializer valuej expression");
                 }

                 var fieldValue = PopExpression()
                                  ?? throw new InvalidOperationException("Expected field initializer value expression");

                 return new FieldInitializer(fieldName, fieldValue);
            });
    }

    private ObjectInitializerExpression GetObjectInitializer(TypeIdentifier type)
    {
        return new ObjectInitializerExpression(new ObjectInitializer(type, GetFieldInitializers()));
    }

    private MethodReturnExpression GetMethodReturn()
    {
        var valueExpression = MoveNext()
            ? PopExpression()
            : null;
            
        return new MethodReturnExpression(new MethodReturn(valueExpression));
    }

    /// <summary>
    /// Pops the next expression.
    /// Expects Current to be on the first token of the expression
    /// </summary>
    /// <param name="currentBindingStrength"></param>
    /// <returns></returns>
    private IExpression? PopExpression(uint? currentBindingStrength = null)
    {
        IExpression? previousExpression = null;
        var keepBinding = true;
        while (keepBinding && Current.Type != TokenType.Semicolon)
        {
            previousExpression = MatchTokenToExpression(previousExpression);

            keepBinding = _hasNext
                          && TryGetBindingStrength(Current, out var bindingStrength)
                          && bindingStrength > (currentBindingStrength ?? 0);
        }
        
        return previousExpression;
    }

    private MethodCallExpression GetMethodCall(IExpression method)
    {
        var parameterList = GetCommaSeparatedList(
            TokenType.RightParenthesis,
            "Method Parameter",
            () => PopExpression());

        return new MethodCallExpression(new MethodCall(method, parameterList));
    }

    private IfExpressionExpression GetIfExpression()
    {
        if (!MoveNext())
        {
            throw new InvalidOperationException("Expected left parenthesis, found nothing");
        }

        if (Current.Type != TokenType.LeftParenthesis)
        {
            throw new InvalidOperationException($"Expected left parenthesis, found {Current.Type}");
        }

        if (!MoveNext())
        {
            throw new InvalidOperationException("Expected check expression");
        }

        var checkExpression = PopExpression()
            ?? throw new InvalidOperationException("Expected check expression, found no expression");

        if (!_hasNext || Current.Type != TokenType.RightParenthesis)
        {
            throw new InvalidOperationException("Expected right parenthesis");
        }

        if (!MoveNext())
        {
            throw new InvalidOperationException("Expected if body");
        }

        var body = PopExpression() ??
            throw new InvalidOperationException("Expected if body, found nothing");

        var elseIfs = new List<ElseIf>();
        IExpression? elseBody = null;

        while (_hasNext && Current.Type == TokenType.Else)
        {
            // pop the "else" token off
            if (!MoveNext())
            {
                throw new InvalidOperationException("Expected else body or if");
            }

            if (Current.Type == TokenType.If)
            {
                // pop the "if" token off
                if (!MoveNext() || Current.Type != TokenType.LeftParenthesis)
                {
                    throw new InvalidOperationException("Expected left Parenthesis");
                }

                if (!MoveNext())
                {
                    throw new InvalidOperationException("Expected check expression");
                }

                var elseIfCheckExpression = PopExpression()
                                            ?? throw new InvalidOperationException("Expected check expression");
                
                if (!_hasNext || Current.Type != TokenType.RightParenthesis)
                {
                    throw new InvalidOperationException("Expected right Parenthesis");
                }

                if (!MoveNext())
                {
                    throw new InvalidOperationException("Expected else if body");
                }

                var elseIfBody = PopExpression()
                    ?? throw new InvalidOperationException("Expected else if body");

                elseIfs.Add(new ElseIf(elseIfCheckExpression, elseIfBody));
            }
            else
            {
                elseBody = PopExpression()
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

    private BlockExpression GetBlockExpression()
    {
        var scope = GetScope(TokenType.RightBrace, isUnion: false);

        if (scope.GetScopeTypes().Any(x => x is not (Scope.ScopeType.Expression or Scope.ScopeType.Function)))
        {
            throw new InvalidOperationException("Block expressions can only contain expressions and functions");
        }
        
        return new BlockExpression(new Block(scope.Expressions, scope.Functions));
    }

    private VariableDeclarationExpression GetVariableDeclaration()
    {
        if (!MoveNext())
        {
            throw new InvalidOperationException("Expected variable identifier, got nothing");
        }

        MutabilityModifier? mutabilityModifier = null;

        if (Current.Type == TokenType.Mut)
        {
            mutabilityModifier = new MutabilityModifier(Current);

            if (!MoveNext())
            {
                throw new InvalidOperationException("Expected variable identifier");
            }
        }

        if (Current is not StringToken { Type: TokenType.Identifier } identifier)
        {
            throw new InvalidOperationException($"Expected variable identifier, got {Current}");
        }

        if (!MoveNext())
        {
            throw new InvalidOperationException("Expected = or ; token, got nothing");
        }

        TypeIdentifier? type = null;
        IExpression? valueExpression = null;

        if (Current.Type == TokenType.Colon)
        {
            if (!MoveNext())
            {
                throw new InvalidOperationException("Expected type");
            }

            type = GetTypeIdentifier();
        }
        
        if (_hasNext && Current.Type == TokenType.Equals)
        {
            if (!MoveNext())
            {
                throw new InvalidOperationException("Expected value expression");
            }
            valueExpression = PopExpression()
                        ?? throw new InvalidOperationException("Expected value expression, got nothing");
        }

        return new VariableDeclarationExpression(new VariableDeclaration(identifier, mutabilityModifier, type, valueExpression));
    }
    
    private UnaryOperatorExpression GetUnaryOperatorExpression(
        IExpression operand,
        Token operatorToken,
        UnaryOperatorType operatorType)
    {
        MoveNext();
        
        return new UnaryOperatorExpression(new UnaryOperator(operatorType, operand, operatorToken));
    }
    
    private BinaryOperatorExpression GetBinaryOperatorExpression(
        IExpression leftOperand,
        BinaryOperatorType operatorType)
    {
        var operatorToken = Current;
        if (!TryGetBindingStrength(operatorToken, out var bindingStrength))
        {
            throw new UnreachableException("All operators have a binding strength");
        }

        if (!MoveNext())
        {
            throw new InvalidOperationException("Expected right operand");
        }
        
        var right = PopExpression(bindingStrength.Value);

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
            TokenType.LeftParenthesis => 8,
            TokenType.Turbofish => 7,
            TokenType.Dot => 10,
            TokenType.DoubleColon => 11,
            TokenType.Matches => 1,
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
            BinaryOperatorType.Multiply => 6,
            BinaryOperatorType.Divide => 6,
            BinaryOperatorType.Plus => 5,
            BinaryOperatorType.Minus => 5,
            BinaryOperatorType.GreaterThan => 4,
            BinaryOperatorType.LessThan => 4,
            BinaryOperatorType.EqualityCheck => 3,
            BinaryOperatorType.ValueAssignment => 2,
            _ => throw new InvalidEnumArgumentException(nameof(operatorType), (int)operatorType, typeof(BinaryOperatorType))
        };
    }
    
    private static uint GetUnaryOperatorBindingStrength(UnaryOperatorType operatorType)
    {
        return operatorType switch
        {
            UnaryOperatorType.FallOut => 9,
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