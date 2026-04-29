using System.Text;
using Reef.Core.Expressions;
using Reef.Core.TypeChecking;

namespace Reef.Core;

public record LangModule(
    ModuleId ModuleId,
    IReadOnlyList<IExpression> Expressions,
    IReadOnlyList<LangFunction> Functions,
    IReadOnlyCollection<ProgramClass> Classes,
    IReadOnlyCollection<ProgramUnion> Unions,
    IReadOnlyList<ModuleImport> TopLevelImports)
{
    public bool TypeChecked { get; set; }

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

    public List<TypeChecker.LocalVariable> TopLevelLocalVariables { get; } = [];
    public List<TypeChecker.FunctionSignature> TopLevelLocalFunctions { get; } = [];
}

public record ModuleId(string Value)
{
    public override string ToString() => Value;
}
public record ModulePathSegment(StringToken Identifier, IReadOnlyList<ModulePathSegment> SubSegments, bool UseAll);
public record ModuleImport(bool IsGlobal, ModulePathSegment RootModulePathSegment);

public interface ITypeIdentifier
{
    SourceRange SourceRange { get; }
}

public record TupleTypeIdentifier(IReadOnlyList<ITypeIdentifier> Members, Token? BoxingSpecifier, SourceRange SourceRange) : ITypeIdentifier
{
    public override string ToString()
    {
        return $"({string.Join(", ", Members)})";
    }
}

public record FnTypeIdentifierParameter(ITypeIdentifier ParameterType, bool Mut)
{
    public override string ToString()
    {
        return Mut
            ? $"mut {ParameterType}"
            : ParameterType.ToString() ?? "";
    }
}

public record FnTypeIdentifier(
    IReadOnlyList<FnTypeIdentifierParameter> Parameters,
    ITypeIdentifier? ReturnType,
    Token? ReturnMutabilityModifier,
    BoxingModifier? BoxingModifier,
    SourceRange SourceRange)
    : ITypeIdentifier
{
    public override string ToString()
    {
        var sb = new StringBuilder();
        if (BoxingModifier is not null)
        {
            sb.Append($"{BoxingModifier} ");
        }
        sb.Append("Fn(");
        sb.AppendJoin(", ", Parameters);
        sb.Append(')');
        if (ReturnType is not null)
        {
            sb.Append(": ");
            if (ReturnMutabilityModifier is not null)
            {
                sb.Append($"{ReturnMutabilityModifier} ");
            }
            sb.Append(ReturnType);
        }

        return sb.ToString();
    }
}

public record UnitTypeIdentifier(SourceRange SourceRange) : ITypeIdentifier;

public record ArrayTypeIdentifier(
    ITypeIdentifier ElementTypeIdentifier,
    IntToken? LengthSpecifier,
    Token? BoxingSpecifier,
    SourceRange SourceRange) : ITypeIdentifier
{
    public override string ToString()
    {
        var sb = new StringBuilder();

        if (BoxingSpecifier is not null)
        {
            sb.Append(BoxingSpecifier);
        }

        sb.Append('[');
        sb.Append(ElementTypeIdentifier);
        if (LengthSpecifier is not null)
        {
            sb.Append(';');
            sb.Append(LengthSpecifier.IntValue);
        }
        sb.Append(']');

        return sb.ToString();
    }
}

public record NamedTypeIdentifier(
    StringToken Identifier,
    IReadOnlyList<ITypeIdentifier> TypeArguments,
    Token? BoxedSpecifier,
    IReadOnlyList<StringToken> ModulePath,
    bool ModulePathIsGlobal,
    SourceRange SourceRange) : ITypeIdentifier
{
    public override string ToString()
    {
        var sb = new StringBuilder();

        if (BoxedSpecifier is not null)
        {
            sb.Append(BoxedSpecifier);
            sb.Append(' ');
        }

        if (ModulePathIsGlobal)
        {
            sb.Append(":::");
        }
        foreach (var moduleId in ModulePath)
        {
            sb.Append(moduleId.StringValue);
            sb.Append(":::");
        }

        sb.Append(Identifier.StringValue);

        if (TypeArguments.Count > 0)
        {
            sb.Append("::<");
            sb.AppendJoin(", ", TypeArguments);
            sb.Append('>');
        }

        return sb.ToString();
    }
}

public record ExternModifier(Token Token)
{
    public override string ToString() => Token.ToString();
}

public record LangFunction(
    AccessModifier? AccessModifier,
    StaticModifier? StaticModifier,
    MutabilityModifier? MutabilityModifier,
    StringToken Name,
    IReadOnlyList<StringToken> TypeParameters,
    IReadOnlyList<FunctionParameter> Parameters,
    ITypeIdentifier? ReturnType,
    Token? ReturnMutabilityModifier,
    Block? Block,
    ExternModifier? ExternModifier)
{
    public TypeChecker.FunctionSignature? Signature { get; set; }

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

        if (ExternModifier is not null)
        {
            sb.Append($"{ExternModifier} ");
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
            sb.Append(": ");
            if (ReturnMutabilityModifier is not null)
            {
                sb.Append(ReturnMutabilityModifier);
            }
            sb.Append(ReturnType);
        }

        if (Block is not null)
        {
            sb.Append($"{Block}");
        }

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

public record BoxingModifier(Token Token)
{
    public override string ToString() => Token.ToString();
}

public record FunctionParameter(ITypeIdentifier? Type, MutabilityModifier? MutabilityModifier, StringToken Identifier)
{
    public override string ToString()
    {
        return $"{Type} {MutabilityModifier} {Identifier}";
    }
}
