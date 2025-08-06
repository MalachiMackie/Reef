namespace Reef.Core;

public record ProgramUnion(
    AccessModifier? AccessModifier,
    StringToken Name,
    IReadOnlyList<StringToken> TypeParameters,
    IReadOnlyList<LangFunction> Functions,
    IReadOnlyList<IProgramUnionVariant> Variants)
{
    public TypeChecker.UnionSignature? Signature { get; set; }
}

public interface IProgramUnionVariant
{
    StringToken Name { get; }
}

public record TupleUnionVariant(StringToken Name, IReadOnlyList<ITypeIdentifier> TupleMembers)
    : IProgramUnionVariant;

public record UnitUnionVariant(StringToken Name) : IProgramUnionVariant;

public record ClassUnionVariant : IProgramUnionVariant
{
    public required IReadOnlyList<ClassField> Fields { get; init; }
    public required StringToken Name { get; init; }
}