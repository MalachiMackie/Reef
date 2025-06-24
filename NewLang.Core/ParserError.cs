namespace NewLang.Core;

public record ParserError
{
    public ParserErrorType Type { get; init; }
    public string Message { get; init; }
    public SourcePosition Position { get; init; }

    private ParserError(string message, SourcePosition position, ParserErrorType type)
    {
        Message = message;
        Position = position;
        Type = type;
    }

    public static ParserError VariableDeclaration_MissingIdentifier(SourcePosition sourcePosition) =>
        new ("Identifier expected", sourcePosition, ParserErrorType.VariableDeclaration_MissingIdentifier);
    
    public static ParserError VariableDeclaration_InvalidIdentifier(Token receivedToken) =>
        new ($"Identifier expected, got {receivedToken}", receivedToken.SourceSpan.Position, ParserErrorType.VariableDeclaration_MissingIdentifier);
    
    public static ParserError VariableDeclaration_MissingType(SourcePosition sourcePosition) =>
        new("Type identifier expected", sourcePosition, ParserErrorType.VariableDeclaration_MissingType);
    
    public static ParserError VariableDeclaration_MissingValue(SourcePosition sourcePosition) =>
        new("Value expression expected", sourcePosition, ParserErrorType.VariableDeclaration_MissingValue);
}

public enum ParserErrorType
{
    // ReSharper disable InconsistentNaming
    
    VariableDeclaration_MissingIdentifier,
    VariableDeclaration_MissingType,
    VariableDeclaration_MissingValue,
    
    // ReSharper restore InconsistentNaming
}