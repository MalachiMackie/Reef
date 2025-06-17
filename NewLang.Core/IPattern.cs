namespace NewLang.Core;

public interface IPattern;

public record UnionVariantPattern(TypeIdentifier Type, StringToken VariantName, StringToken? VariableName) : IPattern;

public record UnionTupleVariantPattern(TypeIdentifier Type, StringToken VariantName, IReadOnlyList<IPattern> TupleParamPatterns, StringToken? VariableName) : IPattern;

public record UnionStructVariantPattern(
    TypeIdentifier Type,
    StringToken VariantName,
    IReadOnlyList<KeyValuePair<StringToken, IPattern?>> FieldPatterns,
    bool RemainingFieldsDiscarded,
    StringToken? VariableName) : IPattern;

public record VariableDeclarationPattern(StringToken VariableName) : IPattern;

public record DiscardPattern : IPattern;

public record ClassPattern(
    TypeIdentifier Type,
    IReadOnlyList<KeyValuePair<StringToken, IPattern?>> FieldPatterns,
    bool RemainingFieldsDiscarded,
    StringToken? VariableName) : IPattern;
