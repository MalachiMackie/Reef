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
    
    public static ParserError UnexpectedToken(Token receivedToken)
    {
        return new ParserError(receivedToken, ParserErrorType.UnexpectedToken);
    }
    
    public static ParserError ExpectedType(Token? receivedToken)
    {
        return new ParserError(receivedToken, ParserErrorType.ExpectedType);
    }

    public static ParserError ExpectedExpression(Token? receivedToken)
    {
        return new ParserError(receivedToken, ParserErrorType.ExpectedExpression);
    }

    public static ParserError Scope_DuplicateModifier(Token token)
    {
        return new ParserError(token, ParserErrorType.Scope_DuplicateModifier);
    }
    
    public static ParserError Function_UnexpectedModifier(Token modifier)
    {
        return new ParserError(modifier, ParserErrorType.Function_UnexpectedModifier);
    }
    
    public static ParserError Class_UnexpectedModifier(Token current)
    {
        return new ParserError(current, ParserErrorType.Class_UnexpectedModifier);
    }
    
    public static ParserError Union_UnexpectedModifier(Token current)
    {
        return new ParserError(current, ParserErrorType.Union_UnexpectedModifier);
    }
}

public enum ParserErrorType
{
    // ReSharper disable InconsistentNaming

    ExpectedToken,
    ExpectedExpression,
    ExpectedType,
    UnexpectedToken,
    ExpectedTokenOrExpression,
    
    Scope_DuplicateModifier,
    
    Function_UnexpectedModifier,
    
    Class_UnexpectedModifier,
    
    Union_UnexpectedModifier,
    
    // ReSharper restore InconsistentNaming
}