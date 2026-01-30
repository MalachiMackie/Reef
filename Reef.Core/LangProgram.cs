using System.Text;
using Reef.Core.Expressions;
using Reef.Core.TypeChecking;

namespace Reef.Core;

public record LangProgram(
    string ModuleId,
    IReadOnlyList<IExpression> Expressions,
    IReadOnlyList<LangFunction> Functions,
    IReadOnlyCollection<ProgramClass> Classes,
    IReadOnlyCollection<ProgramUnion> Unions,
    IReadOnlyList<ModuleImport> TopLevelImports)
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

    public List<TypeChecker.LocalVariable> TopLevelLocalVariables { get; } = [];
    public List<TypeChecker.FunctionSignature> TopLevelLocalFunctions { get; } = [];
}

public record ModuleImport(bool UsGlobal, IReadOnlyList<StringToken> ModuleIdentifiers, bool UseAll);

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
    SourceRange SourceRange)
    : ITypeIdentifier
{
    public override string ToString()
    {
        var sb = new StringBuilder("Fn(");
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
    IntToken LengthSpecifier,
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
        sb.Append(';');
        sb.Append(LengthSpecifier.IntValue);
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

public record LangFunction(
    AccessModifier? AccessModifier,
    StaticModifier? StaticModifier,
    MutabilityModifier? MutabilityModifier,
    StringToken Name,
    IReadOnlyList<StringToken> TypeParameters,
    IReadOnlyList<FunctionParameter> Parameters,
    ITypeIdentifier? ReturnType,
    Token? ReturnMutabilityModifier,
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

public record FunctionParameter(ITypeIdentifier? Type, MutabilityModifier? MutabilityModifier, StringToken Identifier)
{
    public override string ToString()
    {
        return $"{Type} {MutabilityModifier} {Identifier}";
    }
}
