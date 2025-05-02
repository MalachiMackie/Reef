using System.Text;

namespace NewLang.Core;

public readonly record struct LangProgram(
    IReadOnlyList<Expression> Expressions,
    IReadOnlyList<LangFunction> Functions,
    IReadOnlyCollection<ProgramClass> Classes)
{
    public override string ToString()
    {
        var sb = new StringBuilder();

        foreach (var expression in Expressions)
        {
            sb.AppendLine($"{expression};");
        }

        foreach (var function in Functions)
        {
            sb.AppendLine($"{function}");
        }

        foreach (var langClass in Classes)
        {
            sb.AppendLine($"{langClass}");
        }

        return sb.ToString();
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
            sb.Append("::<");
            sb.AppendJoin(", ", TypeArguments);
            sb.Append('>');
        }
        return sb.ToString();
    }
}

public readonly record struct LangFunction(
    AccessModifier? AccessModifier,
    Token Name,
    IReadOnlyList<Token> TypeArguments,
    IReadOnlyList<FunctionParameter> Parameters,
    TypeIdentifier? ReturnType,
    Block Block)
{
    public override string ToString()
    {
        var sb = new StringBuilder();
        if (AccessModifier.HasValue)
        {
            sb.Append($"{AccessModifier} ");
        }
        sb.Append($"fn {Name}");
        if (TypeArguments.Count > 0)
        {
            sb.Append('<');
            sb.AppendJoin(", ", TypeArguments);
            sb.Append('>');
        }
        sb.Append('(');
        sb.AppendJoin(", ", Parameters);
        sb.Append(')');
        if (ReturnType.HasValue)
        {
            sb.Append($": {ReturnType}");
        }
        sb.Append($"{Block}");

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