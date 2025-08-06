using System.Text;

namespace Reef.Core;

public record ProgramClass(
    AccessModifier? AccessModifier,
    StringToken Name,
    IReadOnlyList<StringToken> TypeParameters,
    IReadOnlyCollection<LangFunction> Functions,
    IReadOnlyCollection<ClassField> Fields)
{
    public TypeChecker.ClassSignature? Signature { get; set; }

    public override string ToString()
    {
        var sb = new StringBuilder();
        if (AccessModifier is not null)
        {
            sb.Append($"{AccessModifier} ");
        }

        sb.Append($"class {Name}");
        if (TypeParameters.Count > 0)
        {
            sb.Append('<');
            sb.AppendJoin(", ", TypeParameters);
            sb.Append('>');
        }

        sb.AppendLine(" {");

        foreach (var function in Functions)
        {
            sb.AppendLine($"{function}");
        }

        foreach (var field in Fields)
        {
            sb.AppendLine($"{field};");
        }

        sb.Append('}');

        return sb.ToString();
    }
}

public record ClassField(
    AccessModifier? AccessModifier,
    StaticModifier? StaticModifier,
    MutabilityModifier? MutabilityModifier,
    StringToken Name,
    ITypeIdentifier? Type,
    IExpression? InitializerValue)
{
    public override string ToString()
    {
        var sb = new StringBuilder();

        if (AccessModifier is not null)
        {
            sb.Append($"{AccessModifier} ");
        }

        if (StaticModifier is not null)
        {
            sb.Append($"{StaticModifier} ");
        }

        if (MutabilityModifier is not null)
        {
            sb.Append($"{MutabilityModifier} ");
        }

        sb.Append($"field {Name}: {Type}");
        if (InitializerValue is not null)
        {
            sb.Append($" = {InitializerValue}");
        }

        return sb.ToString();
    }
}