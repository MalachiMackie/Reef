namespace Reef.Core;

public interface IPattern
{
    SourceRange SourceRange { get; }
    
    TypeChecker.ITypeReference? TypeReference { get; }
}

public record UnionVariantPattern(
    ITypeIdentifier Type,
    StringToken? VariantName,
    StringToken? VariableName,
    SourceRange SourceRange) : IPattern
{
    public TypeChecker.ITypeReference? TypeReference { get; set; }
}

public record UnionTupleVariantPattern(
    ITypeIdentifier Type,
    StringToken VariantName,
    IReadOnlyList<IPattern> TupleParamPatterns,
    StringToken? VariableName,
    SourceRange SourceRange) : IPattern
{
    public TypeChecker.ITypeReference? TypeReference { get; set; }
}

public record UnionClassVariantPattern(
    ITypeIdentifier Type,
    StringToken VariantName,
    IReadOnlyList<FieldPattern> FieldPatterns,
    bool RemainingFieldsDiscarded,
    StringToken? VariableName,
    SourceRange SourceRange) : IPattern
{
    public TypeChecker.ITypeReference? TypeReference { get; set; }
}

public record FieldPattern(StringToken FieldName, IPattern? Pattern);

public record VariableDeclarationPattern(StringToken VariableName, SourceRange SourceRange) : IPattern
{
    public TypeChecker.ITypeReference TypeReference => TypeChecker.InstantiatedClass.Never;
}

public record DiscardPattern(SourceRange SourceRange) : IPattern
{
    public TypeChecker.ITypeReference TypeReference => TypeChecker.InstantiatedClass.Never;
}

public record ClassPattern(
    ITypeIdentifier Type,
    IReadOnlyList<FieldPattern> FieldPatterns,
    bool RemainingFieldsDiscarded,
    StringToken? VariableName,
    SourceRange SourceRange) : IPattern
{
    public TypeChecker.ITypeReference? TypeReference { get; set; }
}

public record TypePattern(ITypeIdentifier Type, StringToken? VariableName, SourceRange SourceRange) : IPattern
{
    public TypeChecker.ITypeReference? TypeReference { get; set; }
}