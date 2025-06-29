namespace NewLang.Core;

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
    
    public ParserErrorType Type { get; init; }
    public IReadOnlyList<TokenType>? ExpectedTokenTypes { get; init; }
    public Token? ReceivedToken { get; init; }

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
}