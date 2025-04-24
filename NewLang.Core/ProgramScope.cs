using System.Text;

namespace NewLang.Core;

public readonly record struct ProgramScope(IReadOnlyCollection<Expression> Expressions, IReadOnlyCollection<LangFunction> Functions)
{
    public override string ToString()
    {
        var sb = new StringBuilder();
        foreach (var function in Functions)
        {
            sb.AppendLine($"{function}");
        }
        sb.AppendLine();
        foreach (var expression in Expressions)
        {
            sb.AppendLine($"{expression};");
        }

        return sb.ToString();
    }
}