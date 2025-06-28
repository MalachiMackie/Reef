using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace NewLang.Core;

public sealed class Parser : IDisposable
{
    private readonly List<ParserError> _errors = [];
    private readonly IEnumerator<Token> _tokens;
    private bool _hasNext;

    private Parser(IEnumerable<Token> tokens)
    {
        _tokens = tokens.GetEnumerator();
    }

    private Token Current => _hasNext ? _tokens.Current : throw new InvalidOperationException("No current token");
    private Token LastToken => _hasNext ? throw new InvalidOperationException("Haven't reached the end of the tokens yet") : _tokens.Current;

    public void Dispose()
    {
        _tokens.Dispose();
    }

    public static ParseResult Parse(IEnumerable<Token> tokens)
    {
        using var parser = new Parser(tokens);

        return parser.ParseInner();
    }

    /// <summary>
    ///     static entry point for a single expression. Used for testing
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

    private ParseResult ParseInner()
    {
        if (!MoveNext())
        {
            return new ParseResult(new LangProgram([], [], [], []), []);
        }

        var scope = GetScope(null, [Scope.ScopeType.TypeDefinition, Scope.ScopeType.Expression, Scope.ScopeType.Function]);

        return new ParseResult(
            new LangProgram(scope.Expressions, scope.Functions, scope.Classes, scope.Unions),
            _errors
        );
    }
    
    private Scope GetMemberList(
        HashSet<MemberType> allowedMemberTypes)
    {
        const TokenType closingToken = TokenType.RightBrace;
        var allowVariant = allowedMemberTypes.Contains(MemberType.UnionVariant);
        
        List<TokenType> expectedTokensList = [closingToken];
        foreach (var type in allowedMemberTypes)
        {
            expectedTokensList.AddRange(type switch
            {
                MemberType.Function => [TokenType.Fn, TokenType.Pub, TokenType.Static],
                MemberType.Field => [TokenType.Field, TokenType.Pub, TokenType.Mut, TokenType.Static],
                MemberType.UnionVariant => [TokenType.Identifier],
                _ => throw new ArgumentOutOfRangeException()
            });
        }

        var expectedTokens = expectedTokensList.Distinct().ToArray();

        var start = Current.SourceSpan;
        if (!MoveNext())
        {
            LogError(null, allowComma: false);
            
            return new Scope
            {
                Classes = [],
                Expressions = [],
                Fields = [],
                Functions = [],
                Unions = [],
                Variants = [],
                SourceRange = new SourceRange(start, start)
            };
        }

        Token? foundClosingToken = null;

        var functions = new List<LangFunction>();
        var fields = new List<ClassField>();
        var variants = new List<IProgramUnionVariant>();

        var expectComma = false;

        while (_hasNext)
        {
            if (Current.Type == TokenType.Comma)
            {
                if (!MoveNext())
                {
                    break;
                }

                expectComma = false;
            }
            else if (Current.Type == closingToken)
            {
                foundClosingToken = Current;
                MoveNext();
                break;
            }
            else if (expectComma)
            {
                _errors.Add(ParserError.ExpectedToken(Current, TokenType.Comma, closingToken));
            }
            
            if (Current.Type == closingToken)
            {
                foundClosingToken = Current;
                MoveNext();
                break;
            }

            if (!IsMember(Current.Type, allowVariant))
            {
                LogError(Current, allowComma: false);
                if (!MoveNext())
                {
                    LogError(null, allowComma: false);
                    break;
                }

                continue;
            }

            var (function, field, variant) = GetMember(allowedMemberTypes);
            if (function is not null)
            {
                functions.Add(function);
            }
            else if (field is not null)
            {
                fields.Add(field);
                expectComma = true;
            }
            else if (variant is not null)
            {
                variants.Add(variant);
                expectComma = true;
            }
            
            if (!_hasNext)
            {
                LogError(null, allowComma: true);
                break;
            }
        }

        var endToken = foundClosingToken ?? LastToken;

        return new Scope
        {
            Classes = [],
            Expressions = [],
            Functions = functions,
            Fields = fields,
            Unions = [],
            Variants = variants,
            SourceRange = new SourceRange(start, endToken.SourceSpan)
        };

        void LogError(Token? foundToken, bool allowComma) => _errors.Add(ParserError.ExpectedToken(foundToken, allowComma ? [..expectedTokens, TokenType.Comma] : expectedTokens));
    }

    /// <summary>
    ///     Get a scope. Expects first token to be the token that opened the scope, or the first token in the scope when no
    ///     token opened the scope
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    private Scope GetScope(
        TokenType? closingToken,
        HashSet<Scope.ScopeType> allowedScopeTypes)
    {
        if (allowedScopeTypes.Count == 0)
        {
            throw new InvalidOperationException("Expected at least one allowed scope type");
        }
        List<TokenType> expectedTokensList = [];
        foreach (var type in allowedScopeTypes)
        {
            expectedTokensList.AddRange(type switch
            {
                Scope.ScopeType.Function => [TokenType.Fn, TokenType.Pub, TokenType.Static],
                Scope.ScopeType.TypeDefinition => [TokenType.Class, TokenType.Union, TokenType.Pub],
                Scope.ScopeType.Expression => [],
                _ => throw new ArgumentOutOfRangeException()
            });
        }

        if (closingToken.HasValue)
        {
            expectedTokensList.Add(closingToken.Value);
        }

        var expectedTokens = expectedTokensList.Distinct().ToArray();
        
        Action<Token?> logError = allowedScopeTypes.Contains(Scope.ScopeType.Expression)
            ? foundToken => _errors.Add(ParserError.ExpectedTokenOrExpression(foundToken, expectedTokens))
            : foundToken => _errors.Add(ParserError.ExpectedToken(foundToken, expectedTokens));

        var start = Current.SourceSpan;
        // if there's a closing token, then we're expecting 
        if (closingToken.HasValue && !MoveNext())
        {
            logError(null);
            
            return new Scope
            {
                Classes = [],
                Expressions = [],
                Fields = [],
                Functions = [],
                Unions = [],
                Variants = [],
                SourceRange = new SourceRange(start, start)
            };
        }

        IExpression? tailCallExpression = null;
        Token? foundClosingToken = null;

        var expressions = new List<IExpression>();
        var functions = new List<LangFunction>();
        var fields = new List<ClassField>();
        var classes = new List<ProgramClass>();
        var unions = new List<ProgramUnion>();
        var variants = new List<IProgramUnionVariant>();

        while (_hasNext)
        {
            if (Current.Type == closingToken)
            {
                foundClosingToken = Current;
                MoveNext();
                break;
            }

            var allowableNextTokens = new List<TokenType>();
            if (allowedScopeTypes.Contains(Scope.ScopeType.Function))
                allowableNextTokens.Add(TokenType.Fn);
            if (allowedScopeTypes.Contains(Scope.ScopeType.TypeDefinition))
            {
                allowableNextTokens.AddRange(TokenType.Class, TokenType.Union);
            }

            var (accessModifier, mutabilityModifier, staticModifier) = GetModifiers();

            if (!_hasNext)
            {
                var expectedTokensWithoutModifiers = expectedTokens.Except(new [] {accessModifier?.Token.Type, staticModifier?.Token.Type}.OfType<TokenType>());
                _errors.Add(allowedScopeTypes.Contains(Scope.ScopeType.Expression) && accessModifier is null && staticModifier is null
                    ? ParserError.ExpectedTokenOrExpression(null, [..expectedTokensWithoutModifiers])
                    : ParserError.ExpectedToken(null, [..expectedTokensWithoutModifiers]));
                break;
            }
            
            if (allowedScopeTypes.Contains(Scope.ScopeType.Function) && Current.Type == TokenType.Fn)
            {
                if (mutabilityModifier is not null)
                    _errors.Add(ParserError.UnexpectedModifier(mutabilityModifier.Modifier, TokenType.Pub, TokenType.Static));
                
                var functionDeclaration = GetFunctionDeclaration(accessModifier, staticModifier);
                if (functionDeclaration is not null)
                    functions.Add(functionDeclaration);
                continue;
            }
            
            if (allowedScopeTypes.Contains(Scope.ScopeType.TypeDefinition) && Current.Type is TokenType.Union or TokenType.Class)
            {
                if (mutabilityModifier is not null)
                    _errors.Add(ParserError.UnexpectedModifier(mutabilityModifier.Modifier, TokenType.Pub));
                if (staticModifier is not null)
                    _errors.Add(ParserError.UnexpectedModifier(staticModifier.Token, TokenType.Pub));
                
                var (union, @class) = GetTypeDefinition(accessModifier);
                if (@class is not null)
                {
                    classes.Add(@class);
                }
                else if (union is not null)
                {
                    unions.Add(union);
                }

                if (!_hasNext)
                {
                    if (closingToken.HasValue)
                    {
                        logError(null);
                    }
                    
                    break;
                }

                continue;
            }

            if (!allowedScopeTypes.Contains(Scope.ScopeType.Expression))
            {
                logError(Current);
                
                if (!MoveNext())
                {
                    return new Scope
                    {
                        Classes = [],
                        Expressions = [],
                        Fields = [],
                        Functions = [],
                        Unions = [],
                        Variants = [],
                        SourceRange = new SourceRange(start, start)
                    };
                }
                continue;
            }

            var beforeExpression = Current;
            
            var expression = PopExpression(out var consumedToken);
            if (expression is null)
            {
                if (!consumedToken)
                {
                    logError(Current);
                    MoveNext();
                }
                
                if (!_hasNext || Current.Type == TokenType.Semicolon && !MoveNext())
                {
                    if (closingToken.HasValue)
                    {
                        logError(null);
                    }
                    break;
                }

                if (Current.Type == closingToken)
                {
                    foundClosingToken = Current;
                    MoveNext();
                    break;
                }

                continue;
            }
            
            expressions.Add(expression);

            if (tailCallExpression is not null)
            {
                _errors.Add(ParserError.ExpectedToken(beforeExpression, TokenType.Semicolon));
                tailCallExpression = null;
            }

            if (!_hasNext)
            {
                if (closingToken.HasValue)
                {
                    _errors.Add(ParserError.ExpectedToken(null, TokenType.Semicolon, closingToken.Value));
                }

                break;
            }

            if (Current.Type == TokenType.Semicolon)
            {
                // drop semicolon
                if (!MoveNext() && closingToken.HasValue)
                {
                    logError(null);
                }
            }
            else
            {
                tailCallExpression =
                    expression.ExpressionType is not (ExpressionType.IfExpression or ExpressionType.Block)
                        ? expression
                        : null;
            }

        }

        var endToken = foundClosingToken ?? LastToken;

        return new Scope
        {
            Classes = classes,
            Expressions = expressions,
            Functions = functions,
            Fields = fields,
            Unions = unions,
            Variants = variants,
            SourceRange = new SourceRange(start, endToken.SourceSpan)
        };
    }

    private static bool IsMember(TokenType tokenType, bool allowVariant)
    {
        return tokenType is TokenType.Pub or TokenType.Mut or TokenType.Static or TokenType.Fn or TokenType.Field
               || (allowVariant && tokenType == TokenType.Identifier);
    }

    private (ProgramUnion? Union, ProgramClass? Class) GetTypeDefinition(AccessModifier? accessModifier)
    {
        if (Current.Type == TokenType.Union)
        {
            return (GetUnionDefinition(accessModifier), null);
        }

        if (Current.Type == TokenType.Class)
        {
            return (null, GetClassDefinition(accessModifier));
        }

        _errors.Add(ParserError.ExpectedToken(Current, accessModifier is null
            ? [TokenType.Pub, TokenType.Union, TokenType.Class]
            : [TokenType.Union, TokenType.Class]));
        return (null, null);
    }

    private enum MemberType
    {
        Function,
        Field,
        UnionVariant
    }

    private (AccessModifier? AccessModifier, MutabilityModifier? MutabilityModifier, StaticModifier? StaticModifier)
        GetModifiers()
    {
        AccessModifier? accessModifier = null;
        MutabilityModifier? mutabilityModifier = null;
        StaticModifier? staticModifier = null;

        HashSet<TokenType> expectedTokens = [TokenType.Mut, TokenType.Static, TokenType.Pub];
        
        while (_hasNext && Current.Type is TokenType.Pub or TokenType.Static or TokenType.Mut)
        {
            if (!expectedTokens.Remove(Current.Type))
            {
                _errors.Add(ParserError.DuplicateModifier(Current));
            }
            else
            {
                (accessModifier, staticModifier, mutabilityModifier) = Current.Type switch
                {
                    TokenType.Pub => (new AccessModifier(Current), staticModifier, mutabilityModifier),
                    TokenType.Static => (accessModifier, new StaticModifier(Current), mutabilityModifier),
                    TokenType.Mut => (accessModifier, staticModifier, new MutabilityModifier(Current)),
                    _ => throw new UnreachableException()
                };
            }

            MoveNext();
        }

        return (accessModifier, mutabilityModifier, staticModifier);
    }
    
    private (LangFunction? Function, ClassField? Field, IProgramUnionVariant? Variant) GetMember(
            HashSet<MemberType> allowedScopeTypes)
    {
        if (allowedScopeTypes.Count == 0)
        {
            throw new InvalidOperationException("Expected at least one scope type");
        }
        
        HashSet<TokenType> expectedTokens = [];
        
        if (allowedScopeTypes.Contains(MemberType.Function))
            expectedTokens.Add(TokenType.Fn);
        if (allowedScopeTypes.Contains(MemberType.Field))
            expectedTokens.Add(TokenType.Field);
        if (allowedScopeTypes.Contains(MemberType.UnionVariant))
            expectedTokens.Add(TokenType.Identifier);
        
        var (accessModifier, mutabilityModifier, staticModifier) = GetModifiers( );
        
        if (Current.Type == TokenType.Fn)
        {
            if (mutabilityModifier is not null)
                _errors.Add(ParserError.UnexpectedModifier(mutabilityModifier.Modifier, TokenType.Static, TokenType.Pub));
            
            var function = GetFunctionDeclaration(accessModifier, staticModifier);
            return (function, null, null);
        }

        if (Current.Type == TokenType.Field)
        {
            var field = GetField(accessModifier, staticModifier, mutabilityModifier);

            return (null, field, null);
        }

        if (Current is StringToken { Type: TokenType.Identifier } name && allowedScopeTypes.Contains(MemberType.UnionVariant))
        {
            if (mutabilityModifier is not null)
                _errors.Add(ParserError.UnexpectedModifier(mutabilityModifier.Modifier));
            if (staticModifier is not null)
                _errors.Add(ParserError.UnexpectedModifier(staticModifier.Token));
            if (accessModifier is not null)
                _errors.Add(ParserError.UnexpectedModifier(accessModifier.Token));
            
            return (null, null, GetUnionVariant(name));
        }
        
        _errors.Add(ParserError.ExpectedToken(Current, expectedTokens.ToArray()));
        return (null, null, null);
    }

    private IProgramUnionVariant GetUnionVariant(StringToken variantName)
    {
        if (!MoveNext())
        {
            return new UnitStructUnionVariant(variantName);
        }

        if (Current.Type == TokenType.LeftParenthesis)
        {
            var (tupleTypes, _) = GetCommaSeparatedList(
                TokenType.RightParenthesis,
                expectedTokens: [],
                expectExpression: false,
                expectType: true,
                expectPattern: false,
                () =>
                {
                    if (!ExpectCurrentTypeIdentifier(out var identifier))
                    {
                        MoveNext();
                    }
                    return identifier;
                });

            return new TupleUnionVariant(variantName, tupleTypes);
        }

        if (Current.Type == TokenType.LeftBrace)
        {
            var scope = GetMemberList([MemberType.Field]);

            return new StructUnionVariant
            {
                Name = variantName,
                Fields = scope.Fields
            };
        }

        return new UnitStructUnionVariant(variantName);
    }
    
    private bool ExpectCurrentIdentifier([NotNullWhen(true)] out StringToken? identifier, IReadOnlyList<TokenType> otherExpectedTokens)
    {
        if (!_hasNext)
        {
            _errors.Add(ParserError.ExpectedToken(null, [..otherExpectedTokens, TokenType.Identifier]));
            identifier = null;
            return false;
        }
        
        if (Current is not StringToken { Type: TokenType.Identifier } identifierToken)
        {
            _errors.Add(ParserError.ExpectedToken(Current, [..otherExpectedTokens, TokenType.Identifier]));
            identifier = null;
            return false;
        }
        
        identifier = identifierToken;
        return true;
    }

    private bool ExpectNextIdentifier([NotNullWhen(true)] out StringToken? identifier)
    {
        if (!MoveNext())
        {
            _errors.Add(ParserError.ExpectedToken(null, TokenType.Identifier));
            identifier = null;
            return false;
        }
        
        if (Current is not StringToken { Type: TokenType.Identifier } identifierToken)
        {
            _errors.Add(ParserError.ExpectedToken(Current, TokenType.Identifier));
            identifier = null;
            return false;
        }
        
        identifier = identifierToken;
        return true;
    }
    
    private bool ExpectNextToken(TokenType tokenType)
    {
        if (!MoveNext() || Current.Type != tokenType)
        {
            _errors.Add(ParserError.ExpectedToken(_hasNext ? Current : null, tokenType));
            return false;
        }

        return true;
    }
    
    private bool ExpectCurrentTypeIdentifier([NotNullWhen(true)] out TypeIdentifier? typeIdentifier)
    {
        if (!_hasNext)
        {
            _errors.Add(ParserError.ExpectedType(null));
            typeIdentifier = null;
            return false;
        }
        
        typeIdentifier = GetTypeIdentifier();

        return typeIdentifier is not null;
    }

    private bool ExpectNextTypeIdentifier([NotNullWhen(true)] out TypeIdentifier? typeIdentifier)
    {
        if (!MoveNext())
        {
            _errors.Add(ParserError.ExpectedType(null));
            typeIdentifier = null;
            return false;
        }

        typeIdentifier = GetTypeIdentifier();

        return typeIdentifier is not null;
    }

    private bool ExpectNextExpression([NotNullWhen(true)] out IExpression? expression)
    {
        return ExpectNextExpression(null, out expression);
    }
    
    private bool ExpectNextExpression(uint? currentBindingStrength, [NotNullWhen(true)] out IExpression? expression)
    {
        if (!MoveNext())
        {
            _errors.Add(ParserError.ExpectedExpression(null));
            expression = null;
            return false;
        }

        var beforeExpression = Current;
        expression = PopExpression(currentBindingStrength);

        if (expression is null)
        {
            _errors.Add(ParserError.ExpectedExpression(beforeExpression));
        }
        
        return expression is not null;
    }
    
    private ClassField? GetField(AccessModifier? accessModifier, StaticModifier? staticModifier,
        MutabilityModifier? mutabilityModifier)
    {
        if (!ExpectNextIdentifier(out var name))
        {
            return null;
        }
        
        if (!ExpectNextToken(TokenType.Colon))
        {
            return new ClassField(accessModifier, staticModifier, mutabilityModifier, name, null, null);
        }
        
        if (!ExpectNextTypeIdentifier(out var type))
        {
            return new ClassField(accessModifier, staticModifier, mutabilityModifier, name, null, null);
        }

        IExpression? valueExpression = null;

        if (_hasNext && Current.Type == TokenType.Equals)
        {
            ExpectNextExpression(out valueExpression);
        }

        return new ClassField(accessModifier, staticModifier, mutabilityModifier, name, type, valueExpression);
    }

    private (List<StringToken> items, Token? lastToken) GetGenericParameterList()
    {
        return GetCommaSeparatedList(
            TokenType.RightAngleBracket,
            [TokenType.Identifier],
            expectExpression: false,
            expectType: false,
            expectPattern: false,
            () =>
            {
                ExpectCurrentIdentifier(out var typeArgument, [TokenType.RightAngleBracket]);
                MoveNext();

                return typeArgument;
            });
    }

    private ProgramUnion? GetUnionDefinition(AccessModifier? accessModifier)
    {
        if (!ExpectNextIdentifier(out var name))
        {
            MoveNext();
            return null;
        }
        
        if (!MoveNext())
        {
            _errors.Add(ParserError.ExpectedToken(null, TokenType.LeftAngleBracket, TokenType.LeftBrace));
            return new ProgramUnion(accessModifier, name, [], [], []);
        }

        IReadOnlyList<StringToken>? typeArguments = null;

        if (Current.Type == TokenType.LeftAngleBracket)
        {
            (typeArguments, _) = GetGenericParameterList();

            if (!_hasNext)
            {
                _errors.Add(ParserError.ExpectedToken(null, TokenType.LeftBrace));
                return new ProgramUnion(accessModifier, name, typeArguments, [], []);
            }
        }
        
        if (Current.Type != TokenType.LeftBrace)
        {
            _errors.Add(ParserError.ExpectedToken(Current, typeArguments is not null ? [TokenType.LeftBrace] : [TokenType.LeftBrace, TokenType.LeftAngleBracket]));
            MoveNext();
            return new ProgramUnion(accessModifier, name, typeArguments ?? [], [], []);
        }

        typeArguments ??= [];

        var scope = GetMemberList([MemberType.Function, MemberType.UnionVariant]);

        return new ProgramUnion(
            accessModifier,
            name,
            typeArguments,
            scope.Functions,
            scope.Variants);
    }

    private ProgramClass? GetClassDefinition(AccessModifier? accessModifier)
    {
        if (!ExpectNextIdentifier(out var name))
        {
            MoveNext();
            return null;
        }
        
        if (!MoveNext())
        {
            _errors.Add(ParserError.ExpectedToken(null, TokenType.LeftBrace, TokenType.LeftAngleBracket));
            return new ProgramClass(accessModifier, name, [], [], []);
        }

        IReadOnlyList<StringToken>? typeArguments = null;

        if (Current.Type == TokenType.LeftAngleBracket)
        {
            typeArguments = GetGenericParameterList().items;

            if (!_hasNext)
            {
                _errors.Add(ParserError.ExpectedToken(null, TokenType.LeftBrace));
                return new ProgramClass(accessModifier, name, typeArguments, [], []);
            }
        }

        if (Current.Type != TokenType.LeftBrace)
        {
            _errors.Add(ParserError.ExpectedToken(Current, typeArguments is null ? [TokenType.LeftBrace, TokenType.LeftAngleBracket] : [TokenType.LeftBrace]));
            MoveNext();
            return new ProgramClass(accessModifier, name, typeArguments ?? [], [], []);
        }

        typeArguments ??= [];

        var scope = GetMemberList([MemberType.Function, MemberType.Field]);

        return new ProgramClass(accessModifier, name, typeArguments, scope.Functions, scope.Fields);
    }

    private LangFunction? GetFunctionDeclaration(AccessModifier? accessModifier, StaticModifier? staticModifier)
    {
        if (!ExpectNextIdentifier(out var nameToken))
        {
            MoveNext();
            return null;
        }

        if (!MoveNext())
        {
            _errors.Add(ParserError.ExpectedToken(null, TokenType.LeftAngleBracket, TokenType.LeftParenthesis));
            return new LangFunction(accessModifier, staticModifier, nameToken, [], [], null, new Block([], []));
        }

        IReadOnlyList<StringToken>? typeArguments = null;

        if (Current.Type == TokenType.LeftAngleBracket)
        {
            (typeArguments, var lastToken) = GetGenericParameterList();

            if (!_hasNext)
            {
                if (lastToken is {Type: TokenType.RightAngleBracket})
                {
                    _errors.Add(ParserError.ExpectedToken(null, TokenType.LeftParenthesis));
                }
                return new LangFunction(accessModifier, staticModifier, nameToken, typeArguments, [], null,
                    new Block([], []));
            }
        }

        if (Current.Type != TokenType.LeftParenthesis)
        {
            _errors.Add(ParserError.ExpectedToken(Current, typeArguments is null ? [TokenType.LeftParenthesis, TokenType.LeftAngleBracket] : [TokenType.LeftParenthesis]));
            MoveNext();
            return new LangFunction(accessModifier, staticModifier, nameToken, typeArguments ?? [], [], null,
                new Block([], []));
        }
        typeArguments ??= [];

        var (parameterList, parameterListLastToken) = GetCommaSeparatedList(
            TokenType.RightParenthesis,
            expectedTokens: [TokenType.Identifier, TokenType.Mut],
            expectExpression: false,
            expectType: false,
            expectPattern: false,
            () =>
            {
                MutabilityModifier? mutabilityModifier = null;

                if (Current.Type == TokenType.Mut)
                {
                    mutabilityModifier = new MutabilityModifier(Current);
                    if (!MoveNext())
                    {
                        _errors.Add(ParserError.ExpectedToken(null, TokenType.Identifier));
                        return null;
                    }
                }

                if (Current is not StringToken { Type: TokenType.Identifier } parameterName)
                {
                    _errors.Add(ParserError.ExpectedToken(Current, mutabilityModifier is null ? [TokenType.Mut, TokenType.Identifier] : [TokenType.Identifier]));
                    return null;
                }

                if (!ExpectNextToken(TokenType.Colon))
                {
                    return new FunctionParameter(null, mutabilityModifier, parameterName);
                }

                ExpectNextTypeIdentifier(out var parameterType);
                
                return new FunctionParameter(parameterType, mutabilityModifier, parameterName);
            });

        TypeIdentifier? returnType = null;

        if (!_hasNext)
        {
            if (parameterListLastToken?.Type is TokenType.RightParenthesis)
            {
                _errors.Add(ParserError.ExpectedToken(null, TokenType.LeftBrace, TokenType.Colon));
            }

            return new LangFunction(accessModifier, staticModifier, nameToken, typeArguments, parameterList, null,
                new Block([], []));
        }

        if (Current.Type == TokenType.Colon)
        {
            if (!ExpectNextTypeIdentifier(out returnType))
            {
                MoveNext();
                return new LangFunction(accessModifier, staticModifier, nameToken, typeArguments, parameterList, null,
                    new Block([], []));
            }
            
            if (!_hasNext)
            {
                _errors.Add(ParserError.ExpectedToken(null, TokenType.LeftBrace));
                return new LangFunction(accessModifier, staticModifier, nameToken, typeArguments, parameterList, returnType,
                    new Block([], []));
            }
        }

        if (Current.Type != TokenType.LeftBrace)
        {
            _errors.Add(ParserError.ExpectedToken(Current, returnType is null ? [TokenType.Colon, TokenType.LeftBrace] : [TokenType.LeftBrace]));
            MoveNext();
            return new LangFunction(accessModifier, staticModifier, nameToken, typeArguments, parameterList, returnType,
                new Block([], []));
        }

        var scope = GetScope(TokenType.RightBrace, [Scope.ScopeType.Expression, Scope.ScopeType.Function]);

        return new LangFunction(accessModifier, staticModifier, nameToken, typeArguments, parameterList, returnType,
            new Block(scope.Expressions, scope.Functions));
    }

    private TypeIdentifier? GetTypeIdentifier()
    {
        // use manual check instead of `ExpectCurrentIdentifier` so we can display the `ExpectedType` error
        if (Current is not StringToken { Type: TokenType.Identifier } typeIdentifier)
        {
            _errors.Add(ParserError.ExpectedType(Current));
            return null;
        }
        
        var typeArguments = new List<TypeIdentifier>();
        Token? lastToken = null;
        if (MoveNext() && Current.Type == TokenType.Turbofish)
        {
            (typeArguments, lastToken) = GetCommaSeparatedList(
                TokenType.RightAngleBracket,
                expectedTokens: [],
                expectExpression: false,
                expectType: true,
                expectPattern: false,
                GetTypeIdentifier);
        }

        return new TypeIdentifier(typeIdentifier, typeArguments,
            new SourceRange(typeIdentifier.SourceSpan, lastToken?.SourceSpan ?? typeIdentifier.SourceSpan));
    }

    private IExpression? MatchTokenToExpression(IExpression? previousExpression, out bool consumedToken)
    {
        consumedToken = true;
        var expression = Current.Type switch
        {
            // value accessors
            TokenType.StringLiteral or TokenType.IntLiteral or TokenType.True or TokenType.False =>
                GetLiteralExpression(),
            TokenType.Var => GetVariableDeclaration(),
            TokenType.LeftBrace => GetBlockExpression(),
            TokenType.If => GetIfExpression(),
            TokenType.LeftParenthesis => GetParenthesizedExpression(previousExpression),
            TokenType.Return => GetMethodReturn(),
            TokenType.New => GetInitializer(),
            TokenType.Semicolon => throw new UnreachableException("PopExpression should have handled semicolon"),
            TokenType.Dot => GetMemberAccess(previousExpression),
            TokenType.DoubleColon => GetStaticMemberAccess(previousExpression),
            TokenType.Todo or TokenType.Identifier => GetVariableAccess(),
            TokenType.Turbofish => GetGenericInstantiation(previousExpression),
            TokenType.Matches => GetMatchesExpression(previousExpression),
            TokenType.Match => GetMatchExpression(),
            _ when TryGetUnaryOperatorType(Current.Type, out var unaryOperatorType) => GetUnaryOperatorExpression(
                previousExpression, Current, unaryOperatorType.Value),
            _ when TryGetBinaryOperatorType(Current.Type, out var binaryOperatorType) => GetBinaryOperatorExpression(
                previousExpression,
                binaryOperatorType.Value),
            // hack to assign out variable and return null
#pragma warning disable CS0665 // Assignment in conditional expression is always constant
            // ReSharper disable once ConditionalTernaryEqualBranch
            _ => (consumedToken = false) ? null : null
#pragma warning restore CS0665 // Assignment in conditional expression is always constant
        };

        return expression;
    }

    private MatchExpression GetMatchExpression()
    {
        var matchToken = Current;
        if (!MoveNext() || Current.Type != TokenType.LeftParenthesis)
        {
            throw new InvalidOperationException("Expected (");
        }

        if (!MoveNext())
        {
            throw new InvalidOperationException("Expected match expression value");
        }

        var valueExpression = PopExpression()
                              ?? throw new InvalidOperationException("Expected match expression value");

        if (!_hasNext || Current.Type != TokenType.RightParenthesis)
        {
            throw new InvalidOperationException("Expected )");
        }

        if (!MoveNext() || Current.Type != TokenType.LeftBrace)
        {
            throw new InvalidOperationException("Expected {");
        }

        var (arms, lastToken) = GetCommaSeparatedList(
            TokenType.RightBrace,
            expectedTokens: [],
            expectExpression: false,
            expectType: false,
            expectPattern: true,
            () =>
            {
                var pattern = GetPattern()
                    ?? throw new InvalidOperationException("Expected pattern");

                if (!_hasNext || Current.Type != TokenType.EqualsArrow)
                {
                    throw new InvalidOperationException("Expected =>");
                }

                if (!MoveNext())
                {
                    throw new InvalidOperationException("Expected match arm expression");
                }

                var armExpression = PopExpression()
                                    ?? throw new InvalidOperationException("Expected match arm expression");

                return new MatchArm(pattern, armExpression);
            });

        if (arms.Count == 0)
        {
            throw new InvalidOperationException("Expected match expression to contain at least one arm");
        }

        return new MatchExpression(valueExpression, arms, new SourceRange(matchToken.SourceSpan, lastToken?.SourceSpan ?? matchToken.SourceSpan));
    }

    private IExpression GetParenthesizedExpression(IExpression? previousExpression)
    {
        if (previousExpression is not null)
        {
            return GetMethodCall(previousExpression);
        }

        return GetTupleExpression();
    }

    private TupleExpression GetTupleExpression()
    {
        var startToken = Current;
        var (elements, lastToken) =
            GetCommaSeparatedList(
                TokenType.RightParenthesis,
                expectedTokens: [],
                expectExpression: true,
                expectType: false,
                expectPattern: false,
                () => PopExpression());

        return new TupleExpression(elements, new SourceRange(startToken.SourceSpan, lastToken?.SourceSpan ?? startToken.SourceSpan));
    }

    private MatchesExpression? GetMatchesExpression(IExpression? previousExpression)
    {
        var matchesToken = Current;
        if (previousExpression is null)
        {
            _errors.Add(ParserError.ExpectedExpression(Current));
            MoveNext();
            return null;
        }
        
        if (!MoveNext())
        {
            _errors.Add(ParserError.ExpectedPattern(null));
            return new MatchesExpression(previousExpression, null,
                previousExpression.SourceRange with { End = matchesToken.SourceSpan });
        }

        var pattern = GetPattern();

        return new MatchesExpression(previousExpression, pattern, previousExpression.SourceRange with { });
    }

    private IPattern? GetPattern()
    {
        var start = Current.SourceSpan;
        if (Current.Type == TokenType.Underscore)
        {
            var end = Current.SourceSpan;
            MoveNext();

            return new DiscardPattern(new SourceRange(start, end));
        }

        if (Current.Type == TokenType.Var)
        {
            if (!MoveNext() || Current is not StringToken { Type: TokenType.Identifier } variableName)
            {
                throw new InvalidOperationException("Expected variable name");
            }

            var end = Current.SourceSpan;
            MoveNext();

            return new VariableDeclarationPattern(variableName, new SourceRange(start, end));
        }

        if (Current.Type != TokenType.Identifier)
        {
            _errors.Add(ParserError.ExpectedPattern(Current));
            MoveNext();
            return null;
        }

        var type = GetTypeIdentifier()
                   ?? throw new InvalidOperationException("Expected type");

        if (!_hasNext)
        {
            return new ClassPattern(type, [], false, null, type.SourceRange with { Start = start });
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
            return new UnionVariantPattern(type, variantName, null, new SourceRange(start, variantName.SourceSpan));
        }

        if (Current.Type == TokenType.Var)
        {
            if (!MoveNext() || Current is not StringToken { Type: TokenType.Identifier } variableName)
            {
                throw new InvalidOperationException("Expected variable name");
            }

            var end = Current.SourceSpan;
            MoveNext();

            return variantName is null
                ? new ClassPattern(type, [], false, variableName, new SourceRange(start, end))
                : new UnionVariantPattern(type, variantName, variableName, new SourceRange(start, end));
        }

        if (variantName is not null && Current.Type == TokenType.LeftParenthesis)
        {
            var leftParenthesis = Current;
            var (patterns, patternsLastToken) = GetCommaSeparatedList(
                TokenType.RightParenthesis,
                expectedTokens: [],
                expectExpression: false,
                expectType: false,
                expectPattern: true,
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

            return new UnionTupleVariantPattern(type, variantName, patterns, variableName,
                new SourceRange(start, variableName?.SourceSpan ?? patternsLastToken?.SourceSpan ?? leftParenthesis.SourceSpan));
        }

        if (Current.Type == TokenType.LeftBrace)
        {
            var leftBrace = Current;
            var (fieldPatterns, fieldsLastToken) = GetCommaSeparatedList(
                TokenType.RightBrace,
                expectedTokens: [TokenType.Underscore, TokenType.Identifier],
                expectExpression: false,
                expectType: false,
                expectPattern: false,
                () =>
                {
                    if (Current.Type == TokenType.Underscore)
                    {
                        var underscore = Current;
                        MoveNext();
                        return new KeyValuePair<StringToken?, IPattern?>(
                            null,
                            new DiscardPattern(new SourceRange(underscore.SourceSpan, underscore.SourceSpan)));
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

                    return KeyValuePair.Create<StringToken?, IPattern?>(fieldName, pattern);
                });

            var discards = fieldPatterns.Where(x => x.Key is null && x.Value is DiscardPattern)
                .ToArray();

            switch (discards.Length)
            {
                case > 1:
                    throw new InvalidOperationException("Pattern can only have one field discard");
                case 1 when fieldPatterns[^1] is not { Key: null, Value: DiscardPattern }:
                    throw new InvalidOperationException("field discard must be at the end of the pattern");
                default:
                    var newFieldPatterns = fieldPatterns.Where(x => x.Key is not null)
                        .Select(x => KeyValuePair.Create(x.Key!, x.Value))
                        .ToList();

                    return variantName is not null
                        ? new UnionStructVariantPattern(type, variantName, newFieldPatterns, discards.Length == 1, null,
                            new SourceRange(start, fieldsLastToken?.SourceSpan ?? leftBrace.SourceSpan))
                        : new ClassPattern(type, newFieldPatterns, discards.Length == 1, null,
                            new SourceRange(start, fieldsLastToken?.SourceSpan ?? leftBrace.SourceSpan));
            }
        }

        if (variantName is not null)
        {
            return new UnionVariantPattern(type, variantName, null, new SourceRange(start, variantName.SourceSpan));
        }

        return new ClassPattern(type, [], false, null, type.SourceRange);
    }

    private ValueAccessorExpression GetLiteralExpression()
    {
        var expression = new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Literal, Current));

        MoveNext();

        return expression;
    }

    /// <summary>
    ///     Get a comma separated list of items.
    ///     Expects Current to be the opening token.
    ///     tryGetNext will be performed on the second token.
    ///     Current will be left on token after the terminator.
    /// </summary>
    /// <param name="terminator"></param>
    /// <param name="expectPattern"></param>
    /// <param name="tryGetNext"></param>
    /// <param name="expectedTokens"></param>
    /// <param name="expectExpression"></param>
    /// <param name="expectType"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    private (List<T> items, Token? lastToken) GetCommaSeparatedList<T>(
        TokenType terminator,
        HashSet<TokenType> expectedTokens,
        bool expectExpression,
        bool expectType,
        bool expectPattern,
        Func<T?> tryGetNext)
    {
        var expectCount = (expectedTokens.Count != 0 ? 1 : 0)
            + (expectExpression ? 1 : 0)
            + (expectType ? 1 : 0)
            + (expectPattern ? 1 : 0);

        if (expectCount != 1)
        {
            throw new InvalidOperationException("Expected only one expect item");
        }

        if (!MoveNext())
        {
            LogError(null, expectTerminator: true);
            return ([], null);
        }

        var items = new List<T>();
        while (true)
        {
            if (Current.Type == terminator)
            {
                break;
            }

            if (items.Count > 0)
            {
                if (Current.Type != TokenType.Comma)
                {
                    _errors.Add(ParserError.ExpectedToken(Current, TokenType.Comma, terminator));
                    if (!MoveNext())
                    {
                        break;
                    }
                }
                
                if (!MoveNext())
                {
                    LogError(null, expectTerminator: true);
                    break;
                }
            }
            
            while (_hasNext && Current.Type == TokenType.Comma)
            {
                LogError(Current, expectTerminator: true);
                MoveNext();
            }

            if (!_hasNext)
            {
                LogError(null, expectTerminator: true);
                break;
            }

            if (Current.Type == terminator)
            {
                break;
            }

            var next = tryGetNext();

            if (next is not null)
            {
                items.Add(next);
            }

            if (!_hasNext)
            {
                if (next is not null)
                {
                    _errors.Add(ParserError.ExpectedToken(null, TokenType.Comma, terminator));
                }
                break;
            }
        }

        var lastToken = _hasNext ? Current : LastToken;

        MoveNext();

        return (items, lastToken);

        void LogError(Token? current, bool expectTerminator) 
        {
            if (expectedTokens.Count != 0)
            {
                _errors.Add(expectTerminator
                    ? ParserError.ExpectedToken(current, [..expectedTokens, terminator])
                    : ParserError.ExpectedToken(current, [..expectedTokens]));
            }
            else if (expectExpression)
            {
                _errors.Add(expectTerminator
                    ? ParserError.ExpectedTokenOrExpression(current, terminator)
                    : ParserError.ExpectedExpression(current));
            }
            else if (expectType)
            {
                _errors.Add(expectTerminator 
                    ? ParserError.ExpectedTypeOrToken(current, terminator)
                    : ParserError.ExpectedType(current));
            }
            else if (expectPattern)
            {
                throw new NotImplementedException();
            }
        }
    }

    private GenericInstantiationExpression? GetGenericInstantiation(IExpression? previousExpression)
    {
        if (previousExpression is null)
        {
            _errors.Add(ParserError.ExpectedExpression(Current));
            MoveNext();
            return null;
        }

        var firstToken = Current;
        var (typeArguments, lastToken) =
            GetCommaSeparatedList(
                TokenType.RightAngleBracket,
                expectedTokens: [],
                expectExpression: false,
                expectType: true,
                expectPattern: false,
                () =>
                {
                    ExpectCurrentTypeIdentifier(out var typeIdentifier);
                    return typeIdentifier;
                });

        return new GenericInstantiationExpression(new GenericInstantiation(previousExpression, typeArguments),
            previousExpression.SourceRange with { End = lastToken?.SourceSpan ?? firstToken.SourceSpan });
    }

    private ValueAccessorExpression GetVariableAccess()
    {
        var variableToken = Current;

        MoveNext();

        return new ValueAccessorExpression(new ValueAccessor(ValueAccessType.Variable, variableToken));
    }

    private StaticMemberAccessExpression? GetStaticMemberAccess(
        IExpression? previousExpression)
    {
        if (previousExpression is null)
        {
            _errors.Add(ParserError.ExpectedExpression(Current));
            MoveNext();
            
            return null;
        }
        
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
                    AccessType: ValueAccessType.Variable, Token: StringToken token
                }
            })
        {
            throw new UnreachableException("It should be impossible to get here");
        }

        var type = new TypeIdentifier(token, typeArguments ?? [], previousExpression.SourceRange);

        ExpectNextIdentifier(out var memberName);

        MoveNext();

        return new StaticMemberAccessExpression(new StaticMemberAccess(type, memberName));
    }

    private MemberAccessExpression? GetMemberAccess(IExpression? previousExpression)
    {
        if (previousExpression is null)
        {
            _errors.Add(ParserError.ExpectedExpression(Current));
            MoveNext();
            return null;
        }

        ExpectNextIdentifier(out var memberName);
        
        MoveNext();

        return new MemberAccessExpression(new MemberAccess(previousExpression, memberName));
    }

    private IExpression GetInitializer()
    {
        if (!MoveNext())
        {
            throw new InvalidOperationException("Expected type");
        }

        var type = GetTypeIdentifier()
                   ?? throw new InvalidOperationException("Expected type");

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

        var leftBrace = Current;

        var (fieldInitializers, lastToken) = GetFieldInitializers();

        return new UnionStructVariantInitializerExpression(
            new UnionStructVariantInitializer(type, variantName, fieldInitializers),
            type.SourceRange with { End = lastToken?.SourceSpan ?? leftBrace.SourceSpan });
    }

    private (List<FieldInitializer> fieldInitializers, Token? lastToken) GetFieldInitializers()
    {
        return GetCommaSeparatedList(
            TokenType.RightBrace,
            expectedTokens: [TokenType.Identifier],
            expectExpression: false,
            expectType: false,
            expectPattern: false,
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
                    throw new InvalidOperationException("Expected field initializer value expression");
                }

                var fieldValue = PopExpression()
                                 ?? throw new InvalidOperationException("Expected field initializer value expression");

                return new FieldInitializer(fieldName, fieldValue);
            });
    }

    private ObjectInitializerExpression GetObjectInitializer(TypeIdentifier type)
    {
        var firstToken = Current;
        var (fieldInitializers, lastToken) = GetFieldInitializers();
        return new ObjectInitializerExpression(new ObjectInitializer(type, fieldInitializers),
            type.SourceRange with { End = lastToken?.SourceSpan ?? firstToken.SourceSpan });
    }

    private MethodReturnExpression GetMethodReturn()
    {
        var start = Current.SourceSpan;
        var valueExpression = MoveNext()
            ? PopExpression()
            : null;

        return new MethodReturnExpression(new MethodReturn(valueExpression),
            new SourceRange(start, valueExpression?.SourceRange.End ?? start));
    }
    
    private IExpression? PopExpression(uint? currentBindingStrength = null)
    {
        return PopExpression(out _, currentBindingStrength);
    }

    /// <summary>
    ///     Pops the next expression.
    ///     Expects Current to be on the first token of the expression
    /// </summary>
    /// <param name="consumedToken">Whether pop expression consumed a token or not</param>
    /// <param name="currentBindingStrength">Binding strength of the current expression</param>
    /// <returns></returns>
    private IExpression? PopExpression(out bool consumedToken, uint? currentBindingStrength = null)
    {
        IExpression? previousExpression = null;
        var keepBinding = true;
        consumedToken = false;
        
        while (keepBinding && Current.Type != TokenType.Semicolon)
        {
            previousExpression = MatchTokenToExpression(previousExpression, out consumedToken);

            keepBinding = _hasNext
                          && TryGetBindingStrength(Current, out var bindingStrength)
                          && (!TryGetUnaryOperatorType(Current.Type, out var unaryOperatorType) ||
                              !unaryOperatorType.Value.IsPrefix())
                          && bindingStrength > (currentBindingStrength ?? 0);
        }

        return previousExpression;
    }

    private MethodCallExpression GetMethodCall(IExpression method)
    {
        var leftParenthesis = Current;
        var (parameterList, lastToken) = GetCommaSeparatedList(
            TokenType.RightParenthesis,
            expectedTokens: [],
            expectExpression: true,
            expectType: false,
            expectPattern: false,
            () =>
            {
                var expression = PopExpression(out var consumedToken);
                if (!consumedToken)
                {
                    MoveNext();
                }

                return expression;
            });

        return new MethodCallExpression(new MethodCall(method, parameterList),
            method.SourceRange with { End = lastToken?.SourceSpan ?? leftParenthesis.SourceSpan  });
    }

    private IfExpressionExpression GetIfExpression()
    {
        var start = Current.SourceSpan;
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
                elseBody),
            new SourceRange(start,
                elseBody?.SourceRange.End ?? elseIfs.LastOrDefault()?.Body.SourceRange.End ?? body.SourceRange.End));
    }

    private BlockExpression GetBlockExpression()
    {
        var scope = GetScope(TokenType.RightBrace, [Scope.ScopeType.Expression, Scope.ScopeType.Function]);

        return new BlockExpression(new Block(scope.Expressions, scope.Functions), scope.SourceRange);
    }

    private VariableDeclarationExpression? GetVariableDeclaration()
    {
        var start = Current.SourceSpan;
        if (!MoveNext())
        {
            _errors.Add(ParserError.ExpectedToken(null, TokenType.Mut, TokenType.Identifier));
            return null;
        }

        MutabilityModifier? mutabilityModifier = null;

        if (Current.Type == TokenType.Mut)
        {
            mutabilityModifier = new MutabilityModifier(Current);

            if (!ExpectNextToken(TokenType.Identifier))
            {
                return null;
            }
        }

        if (!ExpectCurrentIdentifier(out var identifier, []))
        {
            return null;
        }

        if (!MoveNext())
        {
            return new VariableDeclarationExpression(
                new VariableDeclaration(identifier, mutabilityModifier, null, null),
                new SourceRange(start, identifier.SourceSpan));
        }

        TypeIdentifier? type = null;
        IExpression? valueExpression = null;
        var lastTokenSpan = identifier.SourceSpan;

        if (Current.Type == TokenType.Colon)
        {
            lastTokenSpan = Current.SourceSpan;
            if (ExpectNextTypeIdentifier(out type))
            {
                lastTokenSpan = type.SourceRange.End;
            }
        }

        if (_hasNext && Current.Type == TokenType.Equals)
        {
            lastTokenSpan = Current.SourceSpan;
            if (!ExpectNextExpression(out valueExpression))
            {
                return new VariableDeclarationExpression(new VariableDeclaration(identifier, mutabilityModifier,
                    type, null), new SourceRange(start, lastTokenSpan));
            }
            
            lastTokenSpan = valueExpression.SourceRange.End;
        }

        return new VariableDeclarationExpression(new VariableDeclaration(identifier, mutabilityModifier, type,
            valueExpression), new SourceRange(start, lastTokenSpan));
    }

    private UnaryOperatorExpression GetUnaryOperatorExpression(
        IExpression? operand,
        Token operatorToken,
        UnaryOperatorType operatorType)
    {
        if (operatorType.IsPrefix())
        {
            if (!ExpectNextExpression(GetUnaryOperatorBindingStrength(operatorType), out operand))
            {
                return new UnaryOperatorExpression(new UnaryOperator(operatorType, null, operatorToken));
            }
        }
        else
        {
            if (operand is null)
            {
                _errors.Add(ParserError.ExpectedExpression(Current));
            }

            MoveNext();
        }

        return new UnaryOperatorExpression(new UnaryOperator(operatorType, operand, operatorToken));
    }

    private BinaryOperatorExpression? GetBinaryOperatorExpression(
        IExpression? leftOperand,
        BinaryOperatorType operatorType)
    {
        if (leftOperand is null)
        {
            _errors.Add(ParserError.ExpectedExpression(Current));
            MoveNext();
            return null;
        }

        var operatorToken = Current;
        if (!TryGetBindingStrength(operatorToken, out var bindingStrength))
        {
            throw new UnreachableException("All operators have a binding strength");
        }

        if (!ExpectNextExpression(bindingStrength, out var right))
        {
            return new BinaryOperatorExpression(
                new BinaryOperator(operatorType, leftOperand, null, operatorToken));
        }

        return new BinaryOperatorExpression(new BinaryOperator(operatorType, leftOperand, right, operatorToken));
    }

    private static bool TryGetBindingStrength(Token token, [NotNullWhen(true)] out uint? bindingStrength)
    {
        bindingStrength = token.Type switch
        {
            _ when TryGetUnaryOperatorType(token.Type, out var unaryOperatorType) => GetUnaryOperatorBindingStrength(
                unaryOperatorType.Value),
            _ when TryGetBinaryOperatorType(token.Type, out var binaryOperatorType) => GetBinaryOperatorBindingStrength(
                binaryOperatorType.Value),
            TokenType.LeftParenthesis => 8,
            TokenType.Turbofish => 7,
            TokenType.Dot => 11,
            TokenType.DoubleColon => 12,
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
            TokenType.Bang => UnaryOperatorType.Not,
            _ => null
        };

        return operatorType.HasValue;
    }

    private static bool TryGetBinaryOperatorType(TokenType type,
        [NotNullWhen(true)] out BinaryOperatorType? operatorType)
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
            _ => throw new InvalidEnumArgumentException(nameof(operatorType), (int)operatorType,
                typeof(BinaryOperatorType))
        };
    }

    private static uint GetUnaryOperatorBindingStrength(UnaryOperatorType operatorType)
    {
        return operatorType switch
        {
            UnaryOperatorType.FallOut => 9,
            UnaryOperatorType.Not => 10,
            _ => throw new InvalidEnumArgumentException(nameof(operatorType), (int)operatorType,
                typeof(UnaryOperatorType))
        };
    }

    public record ParseResult(LangProgram ParsedProgram, IReadOnlyList<ParserError> Errors);

    public class Scope
    {
        public enum ScopeType
        {
            Expression,
            Function,
            TypeDefinition,
        }

        public required IReadOnlyList<IExpression> Expressions { get; init; }
        public required IReadOnlyList<LangFunction> Functions { get; init; }
        public required IReadOnlyList<ProgramClass> Classes { get; init; }
        public required IReadOnlyList<ClassField> Fields { get; init; }
        public required IReadOnlyList<ProgramUnion> Unions { get; init; }
        public required IReadOnlyList<IProgramUnionVariant> Variants { get; init; }
        public required SourceRange SourceRange { get; init; }
    }
}