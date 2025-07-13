namespace NewLang.Core;

public interface IPattern
{
    SourceRange SourceRange { get; }
    
    TypeChecker.ITypeReference? TypeReference { get; }
}

public record UnionVariantPattern(
    TypeIdentifier Type,
    StringToken? VariantName,
    StringToken? VariableName,
    SourceRange SourceRange) : IPattern
{
    public TypeChecker.ITypeReference? TypeReference { get; set; }
}

public record UnionTupleVariantPattern(
    TypeIdentifier Type,
    StringToken VariantName,
    IReadOnlyList<IPattern> TupleParamPatterns,
    StringToken? VariableName,
    SourceRange SourceRange) : IPattern
{
    public TypeChecker.ITypeReference? TypeReference { get; set; }
}

public record UnionClassVariantPattern(
    TypeIdentifier Type,
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
    TypeIdentifier Type,
    IReadOnlyList<FieldPattern> FieldPatterns,
    bool RemainingFieldsDiscarded,
    StringToken? VariableName,
    SourceRange SourceRange) : IPattern
{
    public TypeChecker.ITypeReference? TypeReference { get; set; }
}

public record TypePattern(TypeIdentifier Type, StringToken? VariableName, SourceRange SourceRange) : IPattern
{
    public TypeChecker.ITypeReference? TypeReference { get; set; }
}