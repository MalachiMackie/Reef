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
}

public enum TypeCheckerErrorType
{
    MismatchedTypes
}