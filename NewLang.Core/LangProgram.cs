using System.Text;

namespace NewLang.Core;

public readonly record struct LangProgram(ProgramScope Scope)
{
    public override string ToString()
    {
        return Scope.ToString();
    }
}

// todo: is this an ok name?
public readonly record struct TypeIdentifier(Token Identifier, IReadOnlyList<TypeIdentifier> TypeArguments)
{
    public override string ToString()
    {
        var sb = new StringBuilder($"{Identifier}");
        if (TypeArguments.Count > 0)
        {
            sb.Append('<');
            sb.AppendJoin(", ", TypeArguments);
            sb.Append('>');
        }
        return sb.ToString();
    }
}

public readonly record struct LangFunction(
    AccessModifier? AccessModifier,
    Token Name,
    IReadOnlyList<FunctionParameter> Parameters,
    TypeIdentifier? TypeIdentifier,
    ProgramScope FunctionScope)
{
    public override string ToString()
    {
        var sb = new StringBuilder();
        if (AccessModifier.HasValue)
        {
            sb.Append($"{AccessModifier} ");
        }
        sb.Append($"fn {Name}(");
        sb.AppendJoin(", ", Parameters);
        sb.Append(')');
        if (TypeIdentifier.HasValue)
        {
            sb.Append($": {TypeIdentifier}");
        }
        sb.AppendLine(" {");
        sb.Append($"{FunctionScope}");
        sb.Append('}');

        return sb.ToString();
    }
}

public readonly record struct AccessModifier(Token Token)
{
    public override string ToString()
    {
        return $"{Token}";
    }
}

public readonly record struct FunctionParameter(TypeIdentifier Type, Token Identifier)
{
    public override string ToString()
    {
        return $"{Type} {Identifier}";
    }
}