using Reef.Core.TypeChecking;

namespace Reef.Core;

public interface IPattern
{
    SourceRange SourceRange { get; }

    TypeChecker.ITypeReference? TypeReference { get; }
    
    bool IsRedundant { get; set; }
}

public record UnionVariantPattern(
    ITypeIdentifier Type,
    StringToken? VariantName,
    StringToken? VariableName,
    bool IsMutableVariable,
    SourceRange SourceRange) : IPattern
{
    public TypeChecker.ITypeReference? TypeReference { get; set; }
    
    public TypeChecker.LocalVariable? Variable { get; set; }
    public bool IsRedundant { get; set; }
}

public record UnionTupleVariantPattern(
    ITypeIdentifier Type,
    StringToken VariantName,
    IReadOnlyList<IPattern> TupleParamPatterns,
    StringToken? VariableName,
    bool IsMutableVariable,
    SourceRange SourceRange) : IPattern
{
    public TypeChecker.ITypeReference? TypeReference { get; set; }
    
    public TypeChecker.LocalVariable? Variable { get; set; }
    
    public bool IsRedundant { get; set; }
}

public record UnionClassVariantPattern(
    ITypeIdentifier Type,
    StringToken VariantName,
    IReadOnlyList<FieldPattern> FieldPatterns,
    bool RemainingFieldsDiscarded,
    StringToken? VariableName,
    bool IsMutableVariable,
    SourceRange SourceRange) : IPattern
{
    public TypeChecker.ITypeReference? TypeReference { get; set; }
    
    public TypeChecker.LocalVariable? Variable { get; set; }
    
    public bool IsRedundant { get; set; }
}

public record FieldPattern(StringToken FieldName, IPattern? Pattern)
{
    public TypeChecker.LocalVariable? Variable { get; set; }
}

public record VariableDeclarationPattern(
    StringToken VariableName,
    SourceRange SourceRange,
    bool IsMut) : IPattern
{
    public TypeChecker.ITypeReference TypeReference => TypeChecker.InstantiatedClass.Never;
    
    public TypeChecker.LocalVariable? Variable { get; set; }
    
    public bool IsRedundant { get; set; }
}

public record DiscardPattern(SourceRange SourceRange) : IPattern
{
    public TypeChecker.ITypeReference TypeReference => TypeChecker.InstantiatedClass.Never;
    
    public bool IsRedundant { get; set; }

    public override string ToString()
    {
        return "_";
    }
}

public record ClassPattern(
    ITypeIdentifier Type,
    IReadOnlyList<FieldPattern> FieldPatterns,
    bool RemainingFieldsDiscarded,
    StringToken? VariableName,
    bool IsMutableVariable,
    SourceRange SourceRange) : IPattern
{
    public TypeChecker.ITypeReference? TypeReference { get; set; }
    
    public TypeChecker.LocalVariable? Variable { get; set; }
    
    public bool IsRedundant { get; set; }
}

public record TypePattern(ITypeIdentifier Type, StringToken? VariableName, bool IsVariableMutable, SourceRange SourceRange) : IPattern
{
    public TypeChecker.ITypeReference? TypeReference { get; set; }
    
    public TypeChecker.LocalVariable? Variable { get; set; }
    
    public bool IsRedundant { get; set; }
}
