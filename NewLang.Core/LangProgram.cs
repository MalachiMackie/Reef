using System.Text;

namespace NewLang.Core;

public readonly record struct LangProgram(ProgramScope Scope)
{
    public override string ToString()
    {
        return Scope.ToString();
    }
}

public readonly record struct LangFunction(Token Name, IReadOnlyList<FunctionParameter> Parameters, ProgramScope FunctionScope)
{
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append($"fn {Name}(");
        sb.AppendJoin(", ", Parameters);
        sb.AppendLine(") {");
        sb.Append($"{FunctionScope}");
        sb.Append('}');

        return sb.ToString();
    }
}

public readonly record struct FunctionParameter(Token Type, Token Identifier)
{
    public override string ToString()
    {
        return $"{Type} {Identifier}";
    }
}