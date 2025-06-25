namespace NewLang.Core;

public interface IPattern
{
    SourceRange SourceRange { get; }
}

public record UnionVariantPattern(
    TypeIdentifier Type,
    StringToken VariantName,
    StringToken? VariableName,
    SourceRange SourceRange) : IPattern;

public record UnionTupleVariantPattern(
    TypeIdentifier Type,
    StringToken VariantName,
    IReadOnlyList<IPattern> TupleParamPatterns,
    StringToken? VariableName,
    SourceRange SourceRange) : IPattern;

public record UnionStructVariantPattern(
    TypeIdentifier Type,
    StringToken VariantName,
    IReadOnlyList<KeyValuePair<StringToken, IPattern?>> FieldPatterns,
    bool RemainingFieldsDiscarded,
    StringToken? VariableName,
    SourceRange SourceRange) : IPattern;

public record VariableDeclarationPattern(StringToken VariableName, SourceRange SourceRange) : IPattern;

public record DiscardPattern(SourceRange SourceRange) : IPattern;

public record ClassPattern(
    TypeIdentifier Type,
    IReadOnlyList<KeyValuePair<StringToken, IPattern?>> FieldPatterns,
    bool RemainingFieldsDiscarded,
    StringToken? VariableName,
    SourceRange SourceRange) : IPattern;