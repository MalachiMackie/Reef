namespace Reef.Core;

public record ParserError
{
    private ParserError(Token? receivedToken, ParserErrorType type)
    {
        ReceivedToken = receivedToken;
        Type = type;
    }

    private ParserError(Token? receivedToken, IReadOnlyList<TokenType> expectedTokens, ParserErrorType type)
    {
        ExpectedTokenTypes = expectedTokens.Order().ToArray();
        ReceivedToken = receivedToken;
        Type = type;
    }

    public ParserErrorType Type { get; }
    public IReadOnlyList<TokenType>? ExpectedTokenTypes { get; }
    public Token? ReceivedToken { get; }

    public string Format()
    {
        if (Type == ParserErrorType.DuplicateModifier)
        {
            return $"Duplicate modifier {ReceivedToken.NotNull()}";
        }

        var expectedMessage = Type switch
        {
            ParserErrorType.ExpectedToken when ExpectedTokenTypes is { Count: > 1 } =>
                $"Expected one of [{string.Join(", ", ExpectedTokenTypes)}]",
            ParserErrorType.ExpectedToken => $"Expected {ExpectedTokenTypes.NotNull()[0]}",
            ParserErrorType.ExpectedExpression => "Expected expression",
            ParserErrorType.ExpectedType => "Expected type",
            ParserErrorType.ExpectedPattern => "Expected pattern",
            ParserErrorType.ExpectedPatternOrToken when ExpectedTokenTypes is { Count: > 1 } =>
                $"Expected pattern or one of [{string.Join(", ", ExpectedTokenTypes)}]",
            ParserErrorType.ExpectedPatternOrToken => $"Expected pattern or {ExpectedTokenTypes.NotNull()[0]}",
            ParserErrorType.ExpectedTypeOrToken when ExpectedTokenTypes is { Count: > 1 } =>
                $"Expected type or one of [{string.Join(", ", ExpectedTokenTypes)}]",
            ParserErrorType.ExpectedTypeOrToken => $"Expected type or {ExpectedTokenTypes.NotNull()[0]}",
            ParserErrorType.ExpectedTokenOrExpression when ExpectedTokenTypes is { Count: > 1 } =>
                $"Expected expression or one of [{string.Join(", ", ExpectedTokenTypes)}]",
            ParserErrorType.ExpectedTokenOrExpression => $"Expected expression or {ExpectedTokenTypes.NotNull()[0]}",
            ParserErrorType.ExpectedTypeName => "Expected type name",
            ParserErrorType.UnexpectedModifier when ExpectedTokenTypes is { Count: > 1 } =>
                $"Expected one of modifiers [{string.Join(", ", ExpectedTokenTypes)}]",
            ParserErrorType.UnexpectedModifier => $"Expected modifier {ExpectedTokenTypes.NotNull()[0]}",
            _ => throw new ArgumentOutOfRangeException()
        };

        return expectedMessage + $", but received {ReceivedToken?.ToString() ?? "EOF"}";
    }

    public static ParserError ExpectedToken(Token? receivedToken, params IReadOnlyList<TokenType> expectedTokens)
    {
        if (expectedTokens.Count == 0)
        {
            throw new InvalidOperationException("Expected at least one token type");
        }

        return new ParserError(receivedToken, expectedTokens, ParserErrorType.ExpectedToken);
    }

    public static ParserError ExpectedTokenOrExpression(Token? receivedToken, params IReadOnlyList<TokenType> expectedTokens)
    {
        if (expectedTokens.Count == 0)
        {
            throw new InvalidOperationException("Expected at least one token type");
        }

        return new ParserError(receivedToken, expectedTokens, ParserErrorType.ExpectedTokenOrExpression);
    }

    public static ParserError ExpectedType(Token? receivedToken)
    {
        return new ParserError(receivedToken, ParserErrorType.ExpectedType);
    }
    
    public static ParserError ExpectedTypeName(Token? receivedToken)
    {
        return new ParserError(receivedToken, ParserErrorType.ExpectedTypeName);
    }

    public static ParserError ExpectedTypeOrToken(Token? receivedToken, params IReadOnlyList<TokenType> expectedTokens)
    {
        return new ParserError(receivedToken, expectedTokens, ParserErrorType.ExpectedTypeOrToken);
    }

    public static ParserError ExpectedExpression(Token? receivedToken)
    {
        return new ParserError(receivedToken, ParserErrorType.ExpectedExpression);
    }

    public static ParserError UnexpectedModifier(Token receivedToken, params IReadOnlyList<TokenType> allowedModifiers)
    {
        return new ParserError(receivedToken, allowedModifiers, ParserErrorType.UnexpectedModifier);
    }

    public static ParserError DuplicateModifier(Token receivedToken)
    {
        return new ParserError(receivedToken, ParserErrorType.DuplicateModifier);
    }

    public static ParserError ExpectedPattern(Token? receivedToken)
    {
        return new ParserError(receivedToken, ParserErrorType.ExpectedPattern);
    }
    public static ParserError ExpectedPatternOrToken(Token? receivedToken, params IReadOnlyList<TokenType> tokens)
    {
        return new ParserError(receivedToken, tokens, ParserErrorType.ExpectedPatternOrToken);
    }
}

public enum ParserErrorType
{
    // ReSharper disable InconsistentNaming

    ExpectedToken,
    ExpectedExpression,
    ExpectedType,
    ExpectedPattern,
    ExpectedPatternOrToken,
    ExpectedTypeOrToken,
    ExpectedTokenOrExpression,

    DuplicateModifier,
    UnexpectedModifier,

    // ReSharper restore InconsistentNaming
    ExpectedTypeName
}