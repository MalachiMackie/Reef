namespace NewLang.Core;

public record ParserError
{
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

    public ParserErrorType Type { get; init; }
    public string Message { get; init; }

    public SourceRange Range { get; init; }

    public static ParserError VariableDeclaration_MissingIdentifier(Token token)
    {
        return new ParserError($"Identifier expected, got {token}", token.SourceSpan,
            ParserErrorType.VariableDeclaration_MissingIdentifier);
    }

    public static ParserError VariableDeclaration_InvalidIdentifier(Token receivedToken)
    {
        return new ParserError($"Identifier expected, got {receivedToken}", receivedToken.SourceSpan,
            ParserErrorType.VariableDeclaration_MissingIdentifier);
    }

    public static ParserError VariableDeclaration_MissingType(Token token)
    {
        return new ParserError("Type identifier expected", token.SourceSpan,
            ParserErrorType.VariableDeclaration_MissingType);
    }

    public static ParserError VariableDeclaration_MissingValue(Token token)
    {
        return new ParserError("Value expression expected", token.SourceSpan,
            ParserErrorType.VariableDeclaration_MissingValue);
    }

    public static ParserError BinaryOperator_MissingLeftValue(Token token)
    {
        return new ParserError("Value expression expected", token.SourceSpan,
            ParserErrorType.BinaryOperator_MissingLeftValue);
    }

    public static ParserError BinaryOperator_MissingRightValue(Token token)
    {
        return new ParserError("Value expression expected", token.SourceSpan,
            ParserErrorType.BinaryOperator_MissingRightValue);
    }

    public static ParserError UnaryOperator_MissingValue(Token token)
    {
        return new ParserError("Value expression expected", token.SourceSpan,
            ParserErrorType.UnaryOperator_MissingValue);
    }

    public static ParserError Scope_MissingClosingTag(Token token)
    {
        return new ParserError("Scope missing closing tag", token.SourceSpan, ParserErrorType.Scope_MissingClosingTag);
    }

    public static ParserError Scope_UnexpectedModifier(Token token)
    {
        return new ParserError("Unexpected modifier", token.SourceSpan, ParserErrorType.Scope_UnexpectedModifier);
    }

    public static ParserError Scope_DuplicateModifier(Token token)
    {
        return new ParserError($"Duplicate modifier {token}", token.SourceSpan, ParserErrorType.Scope_DuplicateModifier);
    }

    public static ParserError Scope_UnexpectedComma(Token token)
    {
        return new ParserError("Unexpected comma in scope", token.SourceSpan, ParserErrorType.Scope_UnexpectedComma);
    }

    public static ParserError Scope_ExpectedComma(Token token)
    {
        return new ParserError("expected comma after member", token.SourceSpan, ParserErrorType.Scope_ExpectedComma);
    }

    public static ParserError Scope_EarlyTailReturnExpression(IExpression expression)
    {
        return new ParserError("Tail return expression must be at the end of a block", expression.SourceRange,
            ParserErrorType.Scope_EarlyTailReturnExpression);
    }
    
    public static ParserError Scope_MissingMember(Token current, IReadOnlyList<Parser.Scope.ScopeType> allowedScopeTypes)
    {
        return new ParserError($"Expected member: {string.Join(",", allowedScopeTypes.Order())}", current.SourceSpan,
            ParserErrorType.Scope_MissingMember);
    }

    public static ParserError Function_UnexpectedModifier(Token current)
    {
        return new ParserError($"{current} modifier is invalid for a function", current.SourceSpan, ParserErrorType.Function_UnexpectedModifier);
    }
    
    public static ParserError Class_UnexpectedModifier(Token current)
    {
        return new ParserError($"{current} modifier is invalid for a class", current.SourceSpan, ParserErrorType.Class_UnexpectedModifier);
    }
    
    public static ParserError Union_UnexpectedModifier(Token current)
    {
        return new ParserError($"{current} modifier is invalid for a union", current.SourceSpan, ParserErrorType.Union_UnexpectedModifier);
    }
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
    Scope_UnexpectedModifier,
    Scope_MissingMember,
    
    Scope_DuplicateModifier,
    
    Function_UnexpectedModifier,
    
    Class_UnexpectedModifier,
    
    Union_UnexpectedModifier,

    // ReSharper restore InconsistentNaming
}