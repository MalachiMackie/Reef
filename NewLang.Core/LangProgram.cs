namespace NewLang.Core;

public readonly record struct LangProgram(ProgramScope Scope)
{
    
}

public readonly record struct LangFunction(IReadOnlyCollection<Expression> Expressions)
{
    
}