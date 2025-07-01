namespace NewLang.Core;

public record TypeCheckerError(TypeCheckerErrorType Type, SourceRange Range);

public enum TypeCheckerErrorType
{
    
}