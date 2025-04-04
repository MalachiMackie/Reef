namespace NewLang.Core;

public readonly record struct ProgramScope(IReadOnlyCollection<Expression> Expressions, IReadOnlyCollection<LangFunction> Functions)
{
    
}