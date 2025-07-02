namespace NewLang.Core;

public record TypeCheckerError
{
    public TypeCheckerErrorType Type { get; init; }
    public SourceRange Range { get; init; }
    public string Message { get; init; }
    
    private TypeCheckerError(TypeCheckerErrorType type, SourceRange range, string message)
    {
        Type = type;
        Range = range;
        Message = message;
    }

    public static TypeCheckerError MismatchedTypes(SourceRange range, TypeChecker.ITypeReference expected, TypeChecker.ITypeReference actual) =>
        new(TypeCheckerErrorType.MismatchedTypes, range, $"Expected {expected}, but found {actual}");
    
    public static TypeCheckerError ExpressionNotAssignable(IExpression expression) =>
        new(TypeCheckerErrorType.ExpressionNotAssignable, expression.SourceRange, $"Expression {expression} is not assignable");
    
    public static TypeCheckerError NonMutableAssignment(string variableName, SourceRange sourceRange) =>
        new(TypeCheckerErrorType.NonMutableAssignment, sourceRange, $"Variable {variableName} is not marked as mutable");
    
    public static TypeCheckerError NonMutableMemberAssignment(IExpression memberAccess) =>
        new(TypeCheckerErrorType.NonMutableMemberAssignment, memberAccess.SourceRange, $"member {memberAccess} is not marked as mutable");
    
    public static TypeCheckerError NonMutableMemberOwnerAssignment(IExpression ownerExpression) =>
        new(TypeCheckerErrorType.NonMutableMemberOwnerAssignment, ownerExpression.SourceRange, $"member owner {ownerExpression} is not marked as mutable");

    public static TypeCheckerError SymbolNotFound(Token symbol) =>
        new(TypeCheckerErrorType.SymbolNotFound, new SourceRange(symbol.SourceSpan, symbol.SourceSpan), $"Symbol {symbol} not found");
}

public enum TypeCheckerErrorType
{
    MismatchedTypes,
    ExpressionNotAssignable,
    NonMutableAssignment,
    NonMutableMemberAssignment,
    NonMutableMemberOwnerAssignment,
    SymbolNotFound
}