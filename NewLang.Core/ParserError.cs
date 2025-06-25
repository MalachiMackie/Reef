namespace NewLang.Core;

public record ParserError
{
    public ParserErrorType Type { get; init; }
    public string Message { get; init; }
    
    public SourceRange Range { get; init; }

    private ParserError(string message, SourceSpan tokenSource, ParserErrorType type)
    {
        Message = message;
        Range = new SourceRange(tokenSource, tokenSource);
        Type = type;
    }
    
    private ParserError(string message, SourceRange sourceRange, ParserErrorType type)
    {
        Message = message;
        Range = sourceRange;
        Type = type;
    }

    public static ParserError VariableDeclaration_MissingIdentifier(Token token) =>
        new ($"Identifier expected, got {token}", token.SourceSpan, ParserErrorType.VariableDeclaration_MissingIdentifier);
    
    public static ParserError VariableDeclaration_InvalidIdentifier(Token receivedToken) =>
        new ($"Identifier expected, got {receivedToken}", receivedToken.SourceSpan, ParserErrorType.VariableDeclaration_MissingIdentifier);
    
    public static ParserError VariableDeclaration_MissingType(Token token) =>
        new("Type identifier expected", token.SourceSpan, ParserErrorType.VariableDeclaration_MissingType);
    
    public static ParserError VariableDeclaration_MissingValue(Token token) =>
        new("Value expression expected", token.SourceSpan, ParserErrorType.VariableDeclaration_MissingValue);

    public static ParserError BinaryOperator_MissingLeftValue(Token token) =>
        new("Value expression expected", token.SourceSpan, ParserErrorType.BinaryOperator_MissingLeftValue);
    
    public static ParserError BinaryOperator_MissingRightValue(Token token) =>
        new("Value expression expected", token.SourceSpan, ParserErrorType.BinaryOperator_MissingRightValue);
    
    public static ParserError UnaryOperator_MissingValue(Token token) =>
        new("Value expression expected", token.SourceSpan, ParserErrorType.UnaryOperator_MissingValue);

    public static ParserError Scope_MissingClosingTag(Token token) =>
        new("Scope missing closing tag", token.SourceSpan, ParserErrorType.Scope_MissingClosingTag);

    public static ParserError Scope_UnexpectedComma(Token token) =>
        new("Unexpected comma in scope", token.SourceSpan, ParserErrorType.Scope_UnexpectedComma);
    
    public static ParserError Scope_ExpectedComma(Token token) =>
        new("expected comma after member", token.SourceSpan, ParserErrorType.Scope_ExpectedComma);

    public static ParserError Scope_EarlyTailReturnExpression(IExpression expression) =>
        new("Tail return expression must be at the end of a block", expression.SourceRange, ParserErrorType.Scope_EarlyTailReturnExpression);
}

public enum ParserErrorType
{
    // ReSharper disable InconsistentNaming
    
    VariableDeclaration_MissingIdentifier,
    VariableDeclaration_MissingType,
    VariableDeclaration_MissingValue,
    
    BinaryOperator_MissingLeftValue,
    BinaryOperator_MissingRightValue,
    
    UnaryOperator_MissingValue,
    
    Scope_MissingClosingTag,
    Scope_UnexpectedComma,
    Scope_ExpectedComma,
    Scope_EarlyTailReturnExpression,
    
    // ReSharper restore InconsistentNaming
}