using System.Text;

namespace NewLang.Core;

public readonly record struct ProgramClass(
    AccessModifier? AccessModifier, Token Name, IReadOnlyCollection<LangFunction> Functions, IReadOnlyCollection<ClassField> Fields)
{
    public override string ToString()
    {
        var sb = new StringBuilder();
        if (AccessModifier.HasValue)
        {
            sb.Append($"{AccessModifier.Value} ");
        }

        sb.AppendLine($"class {Name} {{");

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

public readonly record struct ClassField(
    AccessModifier? AccessModifier, MutabilityModifier? MutabilityModifier, Token Name, TypeIdentifier Type)
{
    public override string ToString()
    {
        var sb = new StringBuilder();

        if (AccessModifier.HasValue)
        {
            sb.Append($"{AccessModifier.Value} ");
        }
        if (MutabilityModifier.HasValue)
        {
            sb.Append($"{MutabilityModifier.Value} ");
        }
        sb.Append($"field {Name}: {Type}");

        return sb.ToString();
    }
}