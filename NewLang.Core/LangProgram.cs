using System.Text;

namespace NewLang.Core;

public record LangProgram(
    IReadOnlyList<IExpression> Expressions,
    IReadOnlyList<LangFunction> Functions,
    IReadOnlyCollection<ProgramClass> Classes,
    IReadOnlyCollection<ProgramUnion> Unions)
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
public record TypeIdentifier(StringToken Identifier, IReadOnlyList<TypeIdentifier> TypeArguments, SourceRange SourceRange)
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

public record LangFunction(
    AccessModifier? AccessModifier,
    StaticModifier? StaticModifier,
    StringToken Name,
    IReadOnlyList<StringToken> TypeParameters,
    IReadOnlyList<FunctionParameter> Parameters,
    TypeIdentifier? ReturnType,
    Block Block)
{
    public TypeChecker.FunctionSignature? Signature { get; set; }

    public override string ToString()
    {
        var sb = new StringBuilder();
        if (AccessModifier is not null)
        {
            sb.Append($"{AccessModifier} ");
        }

        sb.Append($"fn {Name}");
        if (TypeParameters.Count > 0)
        {
            sb.Append('<');
            sb.AppendJoin(", ", TypeParameters);
            sb.Append('>');
        }

        sb.Append('(');
        sb.AppendJoin(", ", Parameters);
        sb.Append(')');
        if (ReturnType is not null)
        {
            sb.Append($": {ReturnType}");
        }

        sb.Append($"{Block}");

        return sb.ToString();
    }
}

public record StaticModifier(Token Token)
{
    public override string ToString()
    {
        return $"{Token}";
    }
}

public record AccessModifier(Token Token)
{
    public override string ToString()
    {
        return $"{Token}";
    }
}

public record FunctionParameter(TypeIdentifier? Type, MutabilityModifier? MutabilityModifier, StringToken Identifier)
{
    public override string ToString()
    {
        return $"{Type} {MutabilityModifier} {Identifier}";
    }
}